import { useState } from 'react';
import { Icon, Btn, Field, TextInput, PasswordInput, Flower } from '../components/atoms';
import type { Dir } from '../data';
import { apiFetch } from '../api';



function AuthShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="auth-stage">
      <div className="auth-bg" />
      {children}
    </div>
  );
}

interface LoginFormProps {
  dir: Dir;
  onLogin: () => void;
  onRecovery: () => void;
  workspace?: string;
}

function LoginForm({ dir, onLogin, onRecovery, workspace = 'Balsm' }: LoginFormProps) {
  const isAr = dir === 'rtl';
  const [email, setEmail] = useState('');
  const [pw, setPw] = useState('');
  const [attempts, setAttempts] = useState(0);
  const [err, setErr] = useState<string | false>(false);
  const [locked, setLocked] = useState(false);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    if (locked || loading) return;
    setLoading(true);
    setErr(false);
    try {
      const res = await apiFetch('/api/v1/admin/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: email, password: pw }),
      });
      if (res.ok) { onLogin(); return; }
      if (res.status === 423) {
        const body = await res.json().catch(() => ({}));
        setLocked(true);
        const mins = body.lockoutRemaining ? Math.ceil(body.lockoutRemaining / 60) : 15;
        setErr(isAr ? `تم القفل. حاول بعد ${mins} دقيقة أو استخدم رمز الاسترداد.` : `Account locked. Try again in ${mins} minutes or use your recovery code.`);
      } else {
        const body = await res.json().catch(() => ({}));
        setErr(body.message ?? (isAr ? 'بيانات الدخول غير صحيحة' : 'Incorrect credentials'));
        setAttempts(a => a + 1);
      }
    } catch {
      setErr(isAr ? 'تعذّر الوصول إلى الخادم' : 'Cannot reach the server');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthShell>
      <div className="auth-card">
        <div className="auth-brand">
          <Flower size={48} />
          <span className="wm">Balsm<span className="tld">.health</span></span>
        </div>
        <div className="auth-workspace">
          <Icon name="building-2" size={13} />
          <span>{workspace}</span>
        </div>
        <h1>{isAr ? '\u0644\u0648\u062d\u0629 \u062a\u062d\u0643\u0645 \u0627\u0644\u062e\u0627\u062f\u0645' : 'Server admin'}</h1>
        <p className="lead">{isAr ? '\u0633\u062c\u0651\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0644\u0625\u062f\u0627\u0631\u0629 \u0647\u0630\u0627 \u0627\u0644\u062e\u0627\u062f\u0645' : 'Sign in to manage this server'}</p>

        {locked && (
          <div className="lockout">
            <span className="ic"><Icon name="lock" size={18} /></span>
            <div>
              <b style={{ display: 'block', marginBottom: 2 }}>{isAr ? 'تم القفل مؤقتاً (HTTP 423)' : 'Locked (HTTP 423)'}</b>
              {isAr ? '5 محاولات فاشلة. حاول بعد 15 دقيقة أو استخدم رمز الاسترداد لمرة واحدة.' : '5 failed attempts. Try again in 15 minutes, or use your one-time recovery code.'}
            </div>
          </div>
        )}

        <div className="auth-form" style={{ marginTop: locked ? 16 : 0 }}>
          <Field label={isAr ? '\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a' : 'Email'}>
            <TextInput value={email} onChange={setEmail} type="email" disabled={locked} />
          </Field>
          <Field label={isAr ? '\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631' : 'Password'} error={err && !locked ? err : null}>
            <PasswordInput value={pw} onChange={v => { setPw(v); setErr(false); }} placeholder="\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" onKeyDown={e => e.key === 'Enter' && submit()} disabled={locked} />
          </Field>
          <Btn variant="primary" size="lg" block icon={loading ? 'loader' : 'log-in'} disabled={locked || loading} onClick={submit}>{isAr ? '\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644' : 'Sign in'}</Btn>
        </div>

        <div className="auth-foot">
          {!locked && attempts > 0 && (
            <div style={{ marginBottom: 8, color: 'var(--balsm-danger)' }}>
              {(5 - attempts)} {isAr ? 'محاولات متبقية' : 'attempts remaining'}
            </div>
          )}
          <a onClick={onRecovery}>{isAr ? 'استخدام رمز الاسترداد' : 'Use recovery code'}</a>
          <span style={{ margin: '0 8px', color: 'var(--balsm-ink-300)' }}>·</span>
          <span style={{ color: 'var(--fg3)' }}>{isAr ? 'أو CLI: balsm admin reset-password' : 'or CLI: balsm admin reset-password'}</span>
        </div>
      </div>
    </AuthShell>
  );
}

function RecoveryForm({ dir = 'ltr', onBack, onDone }: { dir: Dir; onBack: () => void; onDone: () => void }) {
  const isAr = dir === 'rtl';
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [pw, setPw] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [apiErr, setApiErr] = useState('');
  // Recovery codes are base64url — keep A–Z a–z 0–9 - _ and only strip whitespace.
  // (A bare alphanumeric strip would delete the '-'/'_' chars that are part of the code.)
  const cleanCode = code.replace(/[^A-Za-z0-9_-]/g, '');
  const valid = email.includes('@') && cleanCode.length >= 16 && pw.length >= 12;

  const submit = async () => {
    if (!valid || submitting) return;
    setSubmitting(true);
    setApiErr('');
    try {
      const res = await apiFetch('/api/v1/admin/auth/recovery/use', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ recoveryCode: cleanCode, newPassword: pw }),
      });
      if (res.ok) { onDone(); return; }
      const body = await res.json().catch(() => ({}));
      setApiErr(body.message ?? (isAr ? 'رمز الاسترداد غير صالح أو مستخدم مسبقاً' : 'Invalid or already-used recovery code'));
    } catch {
      setApiErr(isAr ? 'تعذّر الوصول إلى الخادم' : 'Cannot reach the server');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthShell>
      <div className="auth-card">
        <div className="auth-brand">
          <Flower size={48} />
          <span className="wm">Balsm<span className="tld">.health</span></span>
        </div>
        <h1>{isAr ? 'استرداد الحساب' : 'Account recovery'}</h1>
        <p className="lead">{isAr ? 'أدخل رمز الاسترداد لمرة واحدة الذي حفظته أثناء الإعداد لإعادة تعيين كلمة المرور.' : 'Enter the one-time recovery code you stored during setup to reset your password.'}</p>

        <div className="auth-form">
          <Field label={isAr ? 'البريد الإلكتروني للمسؤول' : 'Admin email'}>
            <TextInput value={email} onChange={setEmail} type="email" placeholder="admin@example.com" autoFocus />
          </Field>
          <Field label={isAr ? 'رمز الاسترداد' : 'Recovery code'} hint={isAr ? '16 حرفاً، يُستخدم مرة واحدة' : '16 characters · single-use'}>
            <TextInput value={code} onChange={setCode} placeholder="XXXX-XXXX-XXXX-XXXX" mono />
          </Field>
          <Field label={isAr ? 'كلمة مرور جديدة' : 'New password'} error={pw && pw.length < 12 ? (isAr ? '12 حرفاً على الأقل' : 'At least 12 characters') : null}>
            <PasswordInput value={pw} onChange={setPw} placeholder="\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022" />
          </Field>
          <div className="callout violet">
            <span className="ic"><Icon name="info" size={16} /></span>
            <div>{isAr ? 'سيُلغي الاسترداد كل الجلسات النشطة ويُسحب هذا الرمز فوراً. يُنشأ رمز جديد عند تسجيل الدخول التالي.' : 'Recovery invalidates all active sessions and retires this code immediately. A fresh code is generated on your next login.'}</div>
          </div>
          {apiErr && <div className="callout danger"><span className="ic"><Icon name="alert-triangle" size={16} /></span><div>{apiErr}</div></div>}
          <Btn variant="primary" size="lg" block icon={submitting ? 'loader' : 'shield-check'} disabled={!valid || submitting} onClick={submit}>{isAr ? 'إعادة التعيين والدخول' : 'Reset & sign in'}</Btn>
        </div>

        <div className="auth-foot">
          <a onClick={onBack}>{isAr ? '← العودة لتسجيل الدخول' : '← Back to sign in'}</a>
        </div>
      </div>
    </AuthShell>
  );
}

export function LoginPage({ onLogin, dir = 'ltr', workspace }: { onLogin: () => void; dir?: Dir; workspace?: string }) {
  const [view, setView] = useState<'login' | 'recovery'>('login');
  if (view === 'recovery') {
    return <RecoveryForm dir={dir} onBack={() => setView('login')} onDone={onLogin} />;
  }
  return <LoginForm dir={dir} onLogin={onLogin} onRecovery={() => setView('recovery')} workspace={workspace} />;
}
