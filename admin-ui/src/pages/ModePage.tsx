import { useState } from 'react';
import { Icon, Btn, Pill, Card, CopyField } from '../components/atoms';
import type { Dir, DeployMode } from '../data';
import { SERVER } from '../data';

function ModeChangeModal({ from, to, dir = 'ltr', onClose, onConfirm }: { from: DeployMode; to: DeployMode; dir?: Dir; onClose: () => void; onConfirm: () => void }) {
  const isAr = dir === 'rtl';
  const labels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة' : 'Network', public: isAr ? 'عام' : 'Public' };
  return (
    <div className="scrim" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <span className="mh-ic info"><Icon name="router" size={22} /></span>
          <div>
            <h2>{isAr ? 'تغيير وضع التشغيل' : 'Change operating mode'}</h2>
            <p>{labels[from]} → <b>{labels[to]}</b></p>
          </div>
        </div>
        <div className="modal-body">
          <div className="callout">
            <span className="ic"><Icon name="rotate-cw" size={16} /></span>
            <div>{isAr ? 'سيعيد مدير الخدمة تشغيل الخادم تلقائياً خلال ~10 ثوانٍ. ستُحدّث اللوحة دون الحاجة لتسجيل الدخول مجدداً.' : 'The service manager restarts the server in-process within ~10s. The panel reloads without you re-authenticating.'}</div>
          </div>
          {to === 'public' && (
            <div className="callout violet">
              <span className="ic"><Icon name="globe" size={16} /></span>
              <div>{isAr ? 'الوضع العام يعرّض الخادم عبر نفق عكسي صادر. تأكد من سياسة المؤسسة قبل المتابعة.' : 'Public mode exposes the server through an outbound reverse tunnel. Confirm your organisation’s policy first.'}</div>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <Btn variant="ghost" onClick={onClose}>{isAr ? 'إلغاء' : 'Cancel'}</Btn>
          <Btn variant={to === 'public' ? 'violet' : 'primary'} icon="check" onClick={onConfirm}>{isAr ? 'تطبيق وإعادة التشغيل' : 'Apply & restart'}</Btn>
        </div>
      </div>
    </div>
  );
}

interface ModePageProps {
  mode: DeployMode;
  onMode: (m: DeployMode) => void;
  dir?: Dir;
}

export function ModePage({ mode, onMode, dir = 'ltr' }: ModePageProps) {
  const isAr = dir === 'rtl';
  const [pending, setPending] = useState<DeployMode | null>(null);

  const cards: { id: DeployMode; ic: string; tag: string; title: string; desc: string }[] = [
    { id: 'standalone', ic: 'monitor', tag: isAr ? 'افتراضي' : 'Default', title: isAr ? 'مستقل' : 'Standalone',
      desc: isAr ? 'يستجيب على هذا الجهاز فقط (localhost). الاكتشاف عبر mDNS معطَّل.' : 'Answers on this machine only (localhost). mDNS discovery suppressed.' },
    { id: 'network', ic: 'network', tag: 'LAN', title: isAr ? 'شبكة' : 'Network',
      desc: isAr ? 'متاح لأجهزة الشبكة المحلية. يُبَث عبر mDNS خلال 5 ثوانٍ.' : 'Reachable by devices on the LAN. Broadcasts over mDNS within 5 seconds.' },
    { id: 'public', ic: 'globe', tag: isAr ? 'متقدم' : 'Advanced', title: isAr ? 'عام' : 'Public',
      desc: isAr ? 'معرّض للإنترنت عبر نفق عكسي صادر. مطلوب موافقة صريحة.' : 'Internet-exposed via an outbound reverse tunnel. Requires explicit opt-in.' },
  ];

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'النظام' : 'System'}</span>
          <h1>{isAr ? 'الوضع والشبكة' : 'Mode & network'}</h1>
          <div className="sub">{isAr ? 'تحكّم في كيفية وصول العملاء إلى الخادم. تغيير الوضع يعيد التشغيل تلقائياً.' : 'Control how clients reach the server. Switching mode triggers an automatic in-process restart.'}</div>
        </div>
      </div>

      <div className="section-stack">
        <div className="mode-grid">
          {cards.map(c => (
            <div key={c.id} className={`mode-card ${mode === c.id ? 'sel' : ''}`} onClick={() => c.id !== mode && setPending(c.id)}>
              <span className="radio" />
              <span className={`mode-ic ${c.id}`}><Icon name={c.ic} size={22} /></span>
              <span className="mode-tag">{c.tag}</span>
              <h4>{c.title}{mode === c.id && <Pill tone="success" dot>{' '}{isAr ? 'نشط' : 'Active'}</Pill>}</h4>
              <p>{c.desc}</p>
            </div>
          ))}
        </div>

        <div className="two-col">
          <Card title={isAr ? 'اكتشاف الشبكة (mDNS)' : 'Network discovery (mDNS)'} icon="radio">
            <div className="kv">
              <div className="kv-row"><span className="k"><Icon name="signal" size={15} /> {isAr ? 'الحالة' : 'Status'}</span>
                <span className="v">{mode === 'standalone' ? <Pill tone="neutral" dot>{isAr ? 'معطَّل' : 'Suppressed'}</Pill> : <Pill tone="success" dot>{isAr ? 'يبث' : 'Broadcasting'}</Pill>}</span></div>
              <div className="kv-row"><span className="k"><Icon name="link" size={15} /> {isAr ? 'العنوان' : 'Address'}</span>
                <span className="v"><CopyField value={`https://${SERVER.host}:${SERVER.httpsPort}`} /></span></div>
              <div className="kv-row"><span className="k"><Icon name="globe-2" size={15} /> {isAr ? 'المضيف' : 'Hostname'}</span>
                <span className="v mono">{SERVER.host}</span></div>
              <div className="kv-row"><span className="k"><Icon name="network" size={15} /> {isAr ? 'عنوان LAN' : 'LAN address'}</span>
                <span className="v mono">{mode === 'standalone' ? '127.0.0.1' : SERVER.ip}</span></div>
            </div>
          </Card>

          <Card title={isAr ? 'الربط والشهادة' : 'Binding & certificate'} icon="lock">
            <div className="kv">
              <div className="kv-row"><span className="k"><Icon name="plug" size={15} /> HTTP</span><span className="v mono">:{SERVER.httpPort}</span></div>
              <div className="kv-row"><span className="k"><Icon name="lock" size={15} /> HTTPS</span><span className="v mono">:{SERVER.httpsPort}</span></div>
              <div className="kv-row"><span className="k"><Icon name="shield-check" size={15} /> TLS</span><span className="v">1.3 · {isAr ? 'موقّعة ذاتياً' : 'self-signed'}</span></div>
              <div className="kv-row"><span className="k"><Icon name="fingerprint" size={15} /> SHA-256</span><span className="v"><CopyField value={SERVER.certSha} /></span></div>
            </div>
          </Card>
        </div>
      </div>

      {pending && <ModeChangeModal from={mode} to={pending} dir={dir} onClose={() => setPending(null)} onConfirm={() => { onMode(pending); setPending(null); }} />}
    </div>
  );
}
