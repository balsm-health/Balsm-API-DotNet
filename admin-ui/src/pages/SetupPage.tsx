import { useState } from 'react';
import { Icon, Btn, Flower, Field, TextInput, PasswordInput } from '../components/atoms';
import type { Dir } from '../data';
import { apiFetch } from '../api';

function slugify(s: string) {
  return s.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 40);
}

interface WizData {
  locale: 'en' | 'ar';
  name: string;
  email: string;
  pw: string;
  ws: string;
  slug: string;
  slugTouched: boolean;
}

type WizStep = 0 | 1 | 2;

function LocaleSection({ data, set, dir = 'ltr' }: { data: WizData; set: (k: keyof WizData, v: string | boolean) => void; dir?: Dir }) {
  const isAr = dir === 'rtl';
  return (
    <div className="wiz-body">
      <div className="locale-grid">
        <div className={`locale-card ${data.locale === 'en' ? 'sel' : ''}`} onClick={() => set('locale', 'en')}>
          <div className="big">English</div>
          <div className="sm">Left-to-right · EGP · DD/MM/YYYY</div>
        </div>
        <div className={`locale-card ${data.locale === 'ar' ? 'sel' : ''}`} onClick={() => set('locale', 'ar')}>
          <div className="big ar">العربية</div>
          <div className="sm">من اليمين لليسار · ج.م</div>
        </div>
      </div>
      <div className="callout">
        <span className="ic"><Icon name="languages" size={16} /></span>
        <div>{isAr ? 'يُطبَّق الاتجاه فوراً ويُحفظ لكل مسؤول. يمكنك تغييره لاحقاً.' : 'Direction applies immediately and is saved per admin. You can change it later.'}</div>
      </div>
    </div>
  );
}

function AccountSection({ data, set, dir = 'ltr' }: { data: WizData; set: (k: keyof WizData, v: string | boolean) => void; dir?: Dir }) {
  const isAr = dir === 'rtl';
  const pwShort = data.pw.length > 0 && data.pw.length < 12;
  return (
    <div className="wiz-body">
      <Field label={isAr ? 'الاسم المعروض' : 'Display name'}>
        <TextInput value={data.name} onChange={v => set('name', v)} placeholder={isAr ? 'مسؤول' : 'Admin User'} />
      </Field>
      <Field label={isAr ? 'البريد الإلكتروني' : 'Email'}>
        <TextInput value={data.email} onChange={v => set('email', v)} type="email" placeholder="admin@example.com" />
      </Field>
      <Field label={isAr ? 'كلمة المرور' : 'Password'} hint={isAr ? <>{'12 حرفاً على الأقل · تُجزَّأ بـ '}<span dir="ltr">Argon2id</span></> : 'At least 12 characters · hashed with Argon2id'} error={pwShort ? (isAr ? 'قصيرة جداً' : 'Too short') : null}>
        <PasswordInput value={data.pw} onChange={v => set('pw', v)} placeholder="••••••••••••" />
      </Field>
      <div className="callout">
        <span className="ic"><Icon name="user-check" size={16} /></span>
        <div>{isAr ? 'يُسمح بمسؤول واحد فقط في هذه المرحلة (FR-018).' : 'Exactly one admin is allowed at this phase (FR-018).'}</div>
      </div>
    </div>
  );
}

function WorkspaceSection({ data, set, dir = 'ltr' }: { data: WizData; set: (k: keyof WizData, v: string | boolean) => void; dir?: Dir }) {
  const isAr = dir === 'rtl';
  return (
    <div className="wiz-body">
      <Field label={isAr ? 'اسم مساحة العمل' : 'Workspace name'}>
        <TextInput value={data.ws} onChange={v => { set('ws', v); if (!data.slugTouched) set('slug', slugify(v)); }} placeholder={isAr ? 'صيدليتي' : 'My Pharmacy'} />
      </Field>
      <Field label={isAr ? 'المُعرّف (slug)' : 'Slug'} hint="^[a-z0-9-]{3,40}$ · mDNS instance name">
        <TextInput value={data.slug} onChange={v => { set('slug', slugify(v)); set('slugTouched', true); }} mono placeholder="my-pharmacy" />
      </Field>
      <div className="callout">
        <span className="ic"><Icon name="link" size={16} /></span>
        <div>{isAr ? <> سيكون الخادم على <span className="mono">{(data.slug || 'workspace')}.balsm.local</span> — يمكن تشغيل عدة خوادم على نفس الجهاز.</> : <>Server will be reachable at <span className="mono">{(data.slug || 'workspace')}.balsm.local</span> — multiple instances can run per machine.</>}</div>
      </div>
    </div>
  );
}

function RecoveryStep({ data, ack, setAck, dir = 'ltr', recoveryCode }: { data: WizData; ack: boolean; setAck: (v: boolean) => void; dir?: Dir; recoveryCode: string }) {
  const isAr = dir === 'rtl';
  const [saved, setSaved] = useState(false);
  const serverUrl = `${data.slug || 'workspace'}.balsm.local`;

  const saveDetails = () => {
    const body = [
      'Balsm — server setup details',
      '────────────────────────────',
      `Workspace:     ${data.ws || '—'}`,
      `Server URL:    https://${serverUrl}:5051`,
      `Admin:         ${data.name || '—'} <${data.email || '—'}>`,
      `Recovery code: ${recoveryCode}`,
      '',
      'Keep this file somewhere safe and offline.',
    ].join('\n');
    try {
      const blob = new Blob([body], { type: 'text/plain' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `balsm-${data.slug || 'workspace'}-setup.txt`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    } catch (_) {}
    setSaved(true);
    setTimeout(() => setSaved(false), 2200);
  };

  return (
    <div className="wiz-body">
      <div className="setup-summary">
        <div className="ss-rows">
          <div className="ss-row">
            <span className="ss-k"><Icon name="building-2" size={15} /> {isAr ? 'مساحة العمل' : 'Workspace'}</span>
            <span className="ss-v">{data.ws || (isAr ? 'مساحة العمل' : 'Untitled workspace')}</span>
          </div>
          <div className="ss-row">
            <span className="ss-k"><Icon name="link" size={15} /> {isAr ? 'عنوان الخادم' : 'Server URL'}</span>
            <span className="ss-v mono">{serverUrl}</span>
          </div>
          <div className="ss-row">
            <span className="ss-k"><Icon name="user-round" size={15} /> {isAr ? 'المسؤول' : 'Admin'}</span>
            <span className="ss-v">{data.email || '—'}</span>
          </div>
        </div>
        <Btn variant="secondary" size="sm" block icon={saved ? 'check' : 'download'} onClick={saveDetails}>
          {saved ? (isAr ? 'تم الحفظ' : 'Saved') : (isAr ? 'حفظ تفاصيل الإعداد' : 'Save setup details')}
        </Btn>
      </div>
      <div className="recovery-code">
        <div className="rc-label">{isAr ? 'رمز الاسترداد لمرة واحدة' : 'One-time recovery code'}</div>
        <div className="rc-code">{recoveryCode || '—'}</div>
        <div className="rc-copy">
          <Btn variant="secondary" size="sm" icon="copy" onClick={() => { try { navigator.clipboard.writeText(recoveryCode); } catch (_) {} }}>
            {isAr ? 'نسخ الرمز' : 'Copy code'}
          </Btn>
        </div>
      </div>
      <div className="callout danger">
        <span className="ic"><Icon name="alert-triangle" size={16} /></span>
        <div>{isAr ? 'يُعرض هذا الرمز مرة واحدة فقط ويُخزَّن كتجزئة فقط. إذا فقدته، الاسترداد الوحيد عبر CLI على الخادم.' : 'This code is shown only once and stored as a salted hash. If you lose it, the only recovery path is the CLI on the server host.'}</div>
      </div>
      <div className={`ack ${ack ? 'checked' : ''}`} onClick={() => setAck(!ack)}>
        <span className="box">{ack && <Icon name="check" size={14} stroke={3} />}</span>
        <span className="txt">{isAr ? 'لقد خزّنت رمز الاسترداد في مكان آمن خارج هذا الجهاز.' : 'I have stored this recovery code somewhere safe, offline, away from this machine.'}</span>
      </div>
    </div>
  );
}

function stepMeta(isAr: boolean) {
  return [
    { icon: 'languages', eyebrow: isAr ? 'الخطوة 1 من 4' : 'Step 1 of 4', railLabel: isAr ? 'اللغة' : 'Language', title: isAr ? 'اختر لغتك' : 'Choose your language', lead: isAr ? 'يدعم بلسم العربية والإنجليزية بالكامل مع تخطيط من اليمين لليسار.' : 'Balsm supports full English and Arabic, with right-to-left layout for Arabic.' },
    { icon: 'user-round', eyebrow: isAr ? 'الخطوة 2 من 4' : 'Step 2 of 4', railLabel: isAr ? 'حساب المسؤول' : 'Admin account', title: isAr ? 'أنشئ حساب المسؤول' : 'Create the admin account', lead: isAr ? 'هذا الحساب يدير الخادم. سيكون المسؤول الوحيد في هذه المرحلة.' : 'This account manages the server. It is the only administrator at this phase.' },
    { icon: 'building-2', eyebrow: isAr ? 'الخطوة 3 من 4' : 'Step 3 of 4', railLabel: isAr ? 'مساحة العمل' : 'Workspace', title: isAr ? 'سمِّ مساحة العمل' : 'Name your workspace', lead: isAr ? 'مساحة عمل واحدة لكل خادم. كل الكيانات والفروع ترتبط بها.' : 'One workspace per server. Every entity and branch anchors to it.' },
    { icon: 'key-round', eyebrow: isAr ? 'الخطوة 4 من 4' : 'Step 4 of 4', railLabel: isAr ? 'رمز الاسترداد' : 'Recovery code', title: isAr ? 'احفظ رمز الاسترداد' : 'Save your recovery code', lead: isAr ? 'طريقك الاحتياطي إن نسيت كلمة المرور أو تم قفل الحساب.' : 'Your fallback if you ever forget your password or get locked out.' },
  ];
}

interface SetupPageProps {
  dir?: Dir;
  onLocale?: (l: 'en' | 'ar') => void;
  onFinish: (locale: 'en' | 'ar') => void;
}

export function SetupPage({ dir = 'ltr', onLocale, onFinish }: SetupPageProps) {
  const isAr = dir === 'rtl';
  const [step, setStep] = useState<WizStep>(0);
  const [phase, setPhase] = useState<'form' | 'recovery'>('form');
  const [ack, setAck] = useState(false);
  const [data, setData] = useState<WizData>({ locale: dir === 'rtl' ? 'ar' : 'en', name: '', email: '', pw: '', ws: '', slug: '', slugTouched: false });
  const [recoveryCode, setRecoveryCode] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [setupError, setSetupError] = useState('');

  const set = (k: keyof WizData, v: string | boolean) => {
    setData(d => ({ ...d, [k]: v }));
    if (k === 'locale') onLocale?.(v as 'en' | 'ar');
  };

  const meta = stepMeta(isAr);
  const railActive = phase === 'recovery' ? 3 : step;

  const stepValid = (i: number): boolean => {
    if (i === 0) return !!data.locale;
    if (i === 1) return !!(data.name && data.email.includes('@') && data.pw.length >= 12);
    if (i === 2) return !!(data.ws && data.slug.length >= 3);
    return true;
  };

  const canAdvance = phase === 'recovery' ? ack : stepValid(step);

  const advance = async () => {
    if (phase === 'recovery') { if (ack) onFinish(data.locale); return; }
    if (step < 2) { setStep((step + 1) as WizStep); return; }
    // step === 2 → submit setup to API
    setSubmitting(true);
    setSetupError('');
    try {
      const res = await apiFetch('/api/v1/admin/auth/setup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: data.email,
          password: data.pw,
          workspaceName: data.ws,
          workspaceSlug: data.slug,
          locale: data.locale,
        }),
      });
      if (res.ok) {
        const body = await res.json();
        setRecoveryCode(body.recoveryCode ?? '');
        setPhase('recovery');
      } else {
        const body = await res.json().catch(() => ({}));
        setSetupError(body.message ?? (isAr ? 'فشل الإعداد. حاول مجدداً.' : 'Setup failed. Please try again.'));
      }
    } catch {
      setSetupError(isAr ? 'تعذّر الوصول إلى الخادم' : 'Cannot reach the server');
    } finally {
      setSubmitting(false);
    }
  };

  const back = () => {
    if (phase === 'recovery') return; // setup already submitted — can't go back
    if (step > 0) setStep((step - 1) as WizStep);
  };

  const m = phase === 'recovery' ? meta[3] : meta[step];

  return (
    <div className="wizard-stage">
      <aside className="wiz-rail">
        <div className="rail-inner">
          <div className="wm">
            <Flower size={34} />
            <span>Balsm<span className="tld">.health</span></span>
          </div>
          <div className="rail-sub">{isAr ? 'إعداد الخادم المحلي · المرحلة 0' : 'Local server setup · Phase 0'}</div>
          <div className="wiz-steps">
            {meta.map((s, i) => {
              const done = i < railActive;
              const active = i === railActive;
              return (
                <div key={i} className={`wiz-step ${active ? 'active' : ''} ${done ? 'done' : ''}`}>
                  <span className="num">{done ? <Icon name="check" size={13} stroke={3} /> : i + 1}</span>
                  <span>{s.railLabel}</span>
                </div>
              );
            })}
          </div>
          <div className="rail-foot">https://balsm.local:5051</div>
        </div>
      </aside>

      <main className="wiz-panel">
        <div className="wiz-panel-inner">
          <div className="step-eyebrow">{phase === 'recovery' ? meta[3].eyebrow : m.eyebrow}</div>
          <h1>{phase === 'recovery' ? meta[3].title : m.title}</h1>
          <p className="step-lead">{phase === 'recovery' ? meta[3].lead : m.lead}</p>

          {phase === 'recovery' ? (
            <RecoveryStep data={data} ack={ack} setAck={setAck} dir={dir} recoveryCode={recoveryCode} />
          ) : step === 0 ? (
            <LocaleSection data={data} set={set} dir={dir} />
          ) : step === 1 ? (
            <AccountSection data={data} set={set} dir={dir} />
          ) : (
            <WorkspaceSection data={data} set={set} dir={dir} />
          )}

          {setupError && (
            <div className="callout danger" style={{ marginTop: 12 }}>
              <span className="ic"><Icon name="alert-triangle" size={16} /></span>
              <div>{setupError}</div>
            </div>
          )}

          <div className="wiz-foot">
            {(step > 0 && phase !== 'recovery') ? (
              <Btn variant="ghost" icon="arrow-left" onClick={back}>{isAr ? 'رجوع' : 'Back'}</Btn>
            ) : <span />}
            <div className="grow" />
            {phase === 'form' && (
              <div className="wiz-progress">
                {[0,1,2,3].map(i => <span key={i} className={i <= step ? 'on' : ''} />)}
              </div>
            )}
            <Btn
              variant="primary"
              icon={phase === 'recovery' ? 'check' : undefined}
              iconRight={phase === 'recovery' ? undefined : (submitting ? undefined : 'arrow-right')}
              disabled={!canAdvance || submitting}
              onClick={advance}
            >
              {submitting ? (isAr ? 'جارٍ الإعداد…' : 'Setting up…') : phase === 'recovery' ? (isAr ? 'إنهاء والدخول للوحة' : 'Finish & open dashboard') : (isAr ? 'متابعة' : 'Continue')}
            </Btn>
          </div>
        </div>
      </main>
    </div>
  );
}
