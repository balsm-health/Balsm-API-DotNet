import { useState } from 'react';
import { Icon, Btn, Pill, Card, Field, TextInput, Switch, Segmented, CopyField } from '../components/atoms';
import type { Dir } from '../data';
import { BACKUPS } from '../data';

function RestoreModal({ backup, dir = 'ltr', onClose, onConfirm }: { backup: typeof BACKUPS[0]; dir?: Dir; onClose: () => void; onConfirm: () => void }) {
  const isAr = dir === 'rtl';
  const [echo, setEcho] = useState('');
  const ok = echo.trim().toUpperCase() === 'RESTORE';
  return (
    <div className="scrim" onClick={onClose}>
      <div className="modal wide" onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <span className="mh-ic danger"><Icon name="history" size={22} /></span>
          <div>
            <h2>{isAr ? 'استعادة من نسخة احتياطية' : 'Restore from backup'}</h2>
            <p>{isAr ? 'سيستبدل هذا قاعدة البيانات الحالية بالكامل ويعيد تشغيل الخادم.' : 'This replaces the entire live database and restarts the server in-process.'}</p>
          </div>
        </div>
        <div className="modal-body">
          <div className="callout">
            <span className="ic"><Icon name="file-box" size={16} /></span>
            <div className="mono" style={{ fontSize: 12, lineHeight: 1.6 }}>
              {backup.name}<br />
              {backup.date} · {backup.size} · SHA-256 {backup.sha}
            </div>
          </div>
          <div className="callout danger">
            <span className="ic"><Icon name="alert-triangle" size={16} /></span>
            <div>{isAr ? 'أثناء الاستعادة سيرفض الخادم كل الطلبات (HTTP 503) عدا فحوصات الصحة. أي بيانات بعد هذه النسخة ستُفقد.' : 'During restore the server returns HTTP 503 for all non-health requests. Any data written after this backup will be lost.'}</div>
          </div>
          <Field label={isAr ? 'اكتب RESTORE للتأكيد' : 'Type RESTORE to confirm'}>
            <TextInput value={echo} onChange={setEcho} placeholder="RESTORE" mono autoFocus />
          </Field>
        </div>
        <div className="modal-foot">
          <Btn variant="ghost" onClick={onClose}>{isAr ? 'إلغاء' : 'Cancel'}</Btn>
          <Btn variant="danger" icon="history" disabled={!ok} onClick={onConfirm}>{isAr ? 'استعادة الآن' : 'Restore now'}</Btn>
        </div>
      </div>
    </div>
  );
}

interface BackupsPageProps {
  dir?: Dir;
  justBackedUp?: boolean;
  onRestore?: () => void;
  onBackupNow?: () => void;
}

export function BackupsPage({ dir = 'ltr', justBackedUp, onRestore, onBackupNow }: BackupsPageProps) {
  const isAr = dir === 'rtl';
  const [restoreTarget, setRestoreTarget] = useState<typeof BACKUPS[0] | null>(null);
  const [schedOn, setSchedOn] = useState(true);
  const [interval, setInterval] = useState('daily');
  const [retention, setRetention] = useState(30);
  const [includeFailed, setIncludeFailed] = useState(true);

  let list = [...BACKUPS];
  if (justBackedUp) {
    const now = new Date();
    list = [{ id: 'bk-new', name: `balsm-2026-05-30_${now.toTimeString().slice(0,5).replace(':','')}.bak`, date: `30/05/2026 ${now.toTimeString().slice(0,5)}`, ago: isAr ? 'الآن' : 'Just now', size: '184.2 MB', kind: 'manual' as const, status: 'ok' as const, sha: 'C1F0…48A2', fresh: true }, ...list];
  }
  const rows = includeFailed ? list : list.filter(b => b.status === 'ok');

  const statusPill = (s: string) => s === 'ok'
    ? <Pill tone="success" dot={false} icon="check">OK</Pill>
    : <Pill tone="danger" dot={false} icon="x">{isAr ? 'فشل' : 'Failed'}</Pill>;

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'البيانات والأمان' : 'Data & safety'}</span>
          <h1>{isAr ? 'النسخ الاحتياطي' : 'Backups'}</h1>
          <div className="sub">{isAr ? 'نسخ احتياطي محلي عبر واجهة SQLite الفورية. الوجهة قابلة للتهيئة.' : "Local backups via SQLite's online backup API. Written to a configurable directory."}</div>
        </div>
        <div className="actions">
          <Btn variant="primary" icon="database-backup" onClick={onBackupNow}>{isAr ? 'نسخ احتياطي الآن' : 'Backup now'}</Btn>
        </div>
      </div>

      <div className="stat-grid c4" style={{ marginBottom: 'var(--gap)' }}>
        <div className="stat"><span className="lab"><Icon name="database-backup" size={13} /> {isAr ? 'إجمالي النسخ' : 'Total backups'}</span><span className="val">{BACKUPS.filter(b=>b.status==='ok').length}</span><span className="foot">{isAr ? 'في المجلد' : 'in backup dir'}</span></div>
        <div className="stat"><span className="lab"><Icon name="clock" size={13} /> {isAr ? 'آخر نجاح' : 'Last success'}</span><span className="val" style={{ fontSize: 22 }}>{isAr ? 'اليوم 02:00' : 'Today 02:00'}</span><span className="foot pos"><Icon name="check" size={12} /> {isAr ? 'مجدول' : 'scheduled'}</span></div>
        <div className="stat"><span className="lab"><Icon name="hard-drive" size={13} /> {isAr ? 'الاستهلاك' : 'Disk used'}</span><span className="val mono">1.1<small> GB</small></span><span className="foot">/var/balsm/backups</span></div>
        <div className="stat"><span className="lab"><Icon name="repeat" size={13} /> {isAr ? 'الاحتفاظ' : 'Retention'}</span><span className="val mono">{retention}</span><span className="foot">{isAr ? 'نسخة' : 'files kept'}</span></div>
      </div>

      <div className="two-col">
        <Card title={isAr ? 'سجل النسخ الاحتياطي' : 'Backup history'} icon="list"
          actions={<label className="meta" style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: 'var(--fg3)', cursor: 'pointer' }}>
            <Switch on={includeFailed} onChange={setIncludeFailed} /> {isAr ? 'إظهار الفاشلة' : 'Show failed'}
          </label>}>
          <div className="card-body flush">
            <table className="balsm">
              <thead>
                <tr>
                  <th>{isAr ? 'الملف' : 'File'}</th>
                  <th>{isAr ? 'النوع' : 'Type'}</th>
                  <th>{isAr ? 'الحجم' : 'Size'}</th>
                  <th>{isAr ? 'الحالة' : 'Status'}</th>
                  <th className="right">{isAr ? 'إجراءات' : 'Actions'}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(b => (
                  <tr key={b.id} style={b.fresh ? { background: 'var(--petal-mint-50)' } : undefined}>
                    <td>
                      <div className="name mono" style={{ fontSize: 12.5 }}>{b.name}</div>
                      <div className="submeta">{b.ago} · SHA {b.sha}</div>
                    </td>
                    <td>{b.kind === 'manual' ? <Pill tone="info" dot={false}>{isAr ? 'يدوي' : 'Manual'}</Pill> : <Pill tone="neutral" dot={false}>{isAr ? 'مجدول' : 'Scheduled'}</Pill>}</td>
                    <td className="num">{b.size}</td>
                    <td>{statusPill(b.status)}</td>
                    <td className="right">
                      <div className="row-actions" style={{ justifyContent: dir === 'rtl' ? 'flex-start' : 'flex-end' }}>
                        <button title={isAr ? 'تنزيل' : 'Download'} disabled={b.status!=='ok'}><Icon name="download" size={15} /></button>
                        <button title={isAr ? 'استعادة' : 'Restore'} disabled={b.status!=='ok'} onClick={() => setRestoreTarget(b)}><Icon name="history" size={15} /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>

        <div className="section-stack">
          <Card title={isAr ? 'النسخ المجدول' : 'Scheduled backups'} icon="calendar-clock"
            actions={<Switch on={schedOn} onChange={setSchedOn} />}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 18, opacity: schedOn ? 1 : .5, pointerEvents: schedOn ? 'auto' : 'none' }}>
              <Field label={isAr ? 'التكرار' : 'Interval'}>
                <Segmented value={interval} onChange={setInterval}
                  options={[{ value: 'daily', label: isAr ? 'يومي' : 'Daily' }, { value: 'weekly', label: isAr ? 'أسبوعي' : 'Weekly' }, { value: 'cron', label: 'Cron' }]} />
              </Field>
              {interval === 'cron'
                ? <Field label={isAr ? 'تعبير Cron' : 'Cron expression'} hint="NCrontab"><TextInput value="0 2 * * *" onChange={() => {}} mono /></Field>
                : <Field label={isAr ? 'الوقت' : 'Run at'} hint={isAr ? 'بتوقيت الخادم المحلي' : 'server-local time'}><TextInput value="02:00" onChange={() => {}} mono /></Field>
              }
              <Field label={isAr ? 'عدد النسخ المحتفظ بها' : 'Retention (files to keep)'} hint={isAr ? 'تُحذف الأقدم بعد نسخة ناجحة' : 'oldest pruned after a successful backup'}>
                <TextInput value={String(retention)} onChange={v => setRetention(Number(v.replace(/\D/g,'')) || 0)} mono />
              </Field>
              <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Btn variant="secondary" size="sm" icon="check">{isAr ? 'حفظ الجدول' : 'Save schedule'}</Btn>
              </div>
            </div>
          </Card>

          <Card title={isAr ? 'الوجهة' : 'Destination'} icon="folder">
            <div className="kv">
              <div className="kv-row"><span className="k"><Icon name="folder-open" size={15} /> {isAr ? 'المجلد' : 'Directory'}</span><span className="v"><CopyField value="/var/balsm/backups" /></span></div>
              <div className="kv-row"><span className="k"><Icon name="cloud-off" size={15} /> {isAr ? 'النسخ السحابي' : 'Cloud replication'}</span><span className="v"><Pill tone="neutral" dot={false}>{isAr ? 'خارج النطاق' : 'Out of scope'}</Pill></span></div>
            </div>
          </Card>
        </div>
      </div>

      {restoreTarget && <RestoreModal backup={restoreTarget} dir={dir} onClose={() => setRestoreTarget(null)} onConfirm={() => { setRestoreTarget(null); onRestore?.(); }} />}
    </div>
  );
}
