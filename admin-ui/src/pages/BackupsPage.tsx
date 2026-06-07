import { useState, useEffect, useCallback } from 'react';
import { Icon, Btn, Pill, Card, Field, TextInput, Switch, Segmented, CopyField } from '../components/atoms';
import type { Dir } from '../data';
import { formatBytesStr, formatTimestamp, shortSha } from '../data';
import { api } from '../api';
import type { BackupFileDto } from '../api';

const isFailed = (status: unknown) => String(status ?? '').toLowerCase().includes('fail');
const isManual = (trigger: unknown) => { const t = String(trigger ?? '').toLowerCase(); return t.includes('manual') || t.includes('demand'); };

function RestoreModal({ backup, dir = 'ltr', onClose, onConfirm }: { backup: BackupFileDto; dir?: Dir; onClose: () => void; onConfirm: () => void }) {
  const isAr = dir === 'rtl';
  const [echo, setEcho] = useState('');
  const [busy, setBusy] = useState(false);
  const ok = echo.trim().toUpperCase() === 'RESTORE';
  const confirm = async () => {
    setBusy(true);
    const res = await api.restoreBackup(backup.id).catch(() => null);
    setBusy(false);
    if (res && res.ok) onConfirm();
  };
  return (
    <div className="scrim" onClick={busy ? undefined : onClose}>
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
              {backup.filename}<br />
              {formatTimestamp(backup.createdAt)} · {formatBytesStr(backup.sizeBytes)} · SHA-256 {shortSha(backup.sha256)}
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
          <Btn variant="ghost" onClick={onClose} disabled={busy}>{isAr ? 'إلغاء' : 'Cancel'}</Btn>
          <Btn variant="danger" icon="history" disabled={!ok || busy} onClick={confirm}>{busy ? (isAr ? 'جارٍ…' : 'Restoring…') : (isAr ? 'استعادة الآن' : 'Restore now')}</Btn>
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

const CRON_DAILY = '0 2 * * *';
const CRON_WEEKLY = '0 2 * * 0';

export function BackupsPage({ dir = 'ltr', justBackedUp, onRestore, onBackupNow }: BackupsPageProps) {
  const isAr = dir === 'rtl';
  const [restoreTarget, setRestoreTarget] = useState<BackupFileDto | null>(null);
  const [list, setList] = useState<BackupFileDto[]>([]);
  const [total, setTotal] = useState(0);
  const [includeFailed, setIncludeFailed] = useState(true);
  const [backingUp, setBackingUp] = useState(false);

  // Schedule
  const [schedOn, setSchedOn] = useState(true);
  const [cron, setCron] = useState(CRON_DAILY);
  const [retention, setRetention] = useState(30);
  const [savingSched, setSavingSched] = useState(false);

  const interval = cron === CRON_DAILY ? 'daily' : cron === CRON_WEEKLY ? 'weekly' : 'cron';

  const reload = useCallback(() => {
    api.getBackups(1, 50).then(r => { setList(r.items); setTotal(r.total); }).catch(() => { setList([]); setTotal(0); });
  }, []);

  useEffect(() => {
    reload();
    api.getBackupSchedule().then(s => { setCron(s.cron); setRetention(s.retention); }).catch(() => {});
  }, [reload]);

  // Auto-trigger if navigated here via "Backup now" from another page
  useEffect(() => {
    if (!justBackedUp) return;
    onBackupNow?.(); // clear the flag in parent
    setBackingUp(true);
    api.triggerBackup().catch(() => null).then(() => { setBackingUp(false); reload(); });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const backupNow = async () => {
    setBackingUp(true);
    await api.triggerBackup().catch(() => null);
    setBackingUp(false);
    reload();
  };

  const saveSchedule = async () => {
    setSavingSched(true);
    await api.updateBackupSchedule(cron, retention).catch(() => null);
    setSavingSched(false);
  };

  const rows = includeFailed ? list : list.filter(b => !isFailed(b.status));
  const okCount = list.filter(b => !isFailed(b.status)).length;
  const lastOk = list.find(b => !isFailed(b.status));
  const diskUsed = list.reduce((sum, b) => sum + (b.sizeBytes || 0), 0);

  const statusPill = (s: string) => !isFailed(s)
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
          <Btn variant="primary" icon="database-backup" disabled={backingUp} onClick={backupNow}>{backingUp ? (isAr ? 'جارٍ…' : 'Backing up…') : (isAr ? 'نسخ احتياطي الآن' : 'Backup now')}</Btn>
        </div>
      </div>

      <div className="stat-grid c4" style={{ marginBottom: 'var(--gap)' }}>
        <div className="stat"><span className="lab"><Icon name="database-backup" size={13} /> {isAr ? 'إجمالي النسخ' : 'Total backups'}</span><span className="val">{total}</span><span className="foot">{okCount} {isAr ? 'ناجحة' : 'successful'}</span></div>
        <div className="stat"><span className="lab"><Icon name="clock" size={13} /> {isAr ? 'آخر نجاح' : 'Last success'}</span><span className="val" style={{ fontSize: 22 }}>{lastOk ? formatTimestamp(lastOk.createdAt).split(' ')[1] : (isAr ? 'لا يوجد' : 'None')}</span><span className="foot pos">{lastOk ? <>{formatTimestamp(lastOk.createdAt).split(' ')[0]}</> : (isAr ? 'لم تُنشأ بعد' : 'no backups yet')}</span></div>
        <div className="stat"><span className="lab"><Icon name="hard-drive" size={13} /> {isAr ? 'الاستهلاك' : 'Disk used'}</span><span className="val mono">{formatBytesStr(diskUsed)}</span><span className="foot">{isAr ? 'في المجلد' : 'in backup dir'}</span></div>
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
                {rows.length === 0 && (
                  <tr><td colSpan={5} style={{ padding: '22px 18px', color: 'var(--fg3)', textAlign: 'center' }}>{isAr ? 'لا توجد نسخ احتياطية بعد.' : 'No backups yet.'}</td></tr>
                )}
                {rows.map(b => {
                  const failed = isFailed(b.status);
                  return (
                    <tr key={b.id}>
                      <td>
                        <div className="name mono" style={{ fontSize: 12.5 }}>{b.filename}</div>
                        <div className="submeta">{formatTimestamp(b.createdAt)} · SHA {shortSha(b.sha256)}</div>
                      </td>
                      <td>{isManual(b.trigger) ? <Pill tone="info" dot={false}>{isAr ? 'يدوي' : 'Manual'}</Pill> : <Pill tone="neutral" dot={false}>{isAr ? 'مجدول' : 'Scheduled'}</Pill>}</td>
                      <td className="num">{formatBytesStr(b.sizeBytes)}</td>
                      <td>{statusPill(b.status)}</td>
                      <td className="right">
                        <div className="row-actions" style={{ justifyContent: dir === 'rtl' ? 'flex-start' : 'flex-end' }}>
                          <button title={isAr ? 'استعادة' : 'Restore'} disabled={failed} onClick={() => setRestoreTarget(b)}><Icon name="history" size={15} /></button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>

        <div className="section-stack">
          <Card title={isAr ? 'النسخ المجدول' : 'Scheduled backups'} icon="calendar-clock"
            actions={<Switch on={schedOn} onChange={setSchedOn} />}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 18, opacity: schedOn ? 1 : .5, pointerEvents: schedOn ? 'auto' : 'none' }}>
              <Field label={isAr ? 'التكرار' : 'Interval'}>
                <Segmented value={interval} onChange={v => setCron(v === 'daily' ? CRON_DAILY : v === 'weekly' ? CRON_WEEKLY : cron)}
                  options={[{ value: 'daily', label: isAr ? 'يومي' : 'Daily' }, { value: 'weekly', label: isAr ? 'أسبوعي' : 'Weekly' }, { value: 'cron', label: 'Cron' }]} />
              </Field>
              <Field label={isAr ? 'تعبير Cron' : 'Cron expression'} hint="NCrontab"><TextInput value={cron} onChange={setCron} mono /></Field>
              <Field label={isAr ? 'عدد النسخ المحتفظ بها' : 'Retention (files to keep)'} hint={isAr ? 'تُحذف الأقدم بعد نسخة ناجحة' : 'oldest pruned after a successful backup'}>
                <TextInput value={String(retention)} onChange={v => setRetention(Number(v.replace(/\D/g, '')) || 0)} mono />
              </Field>
              <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Btn variant="secondary" size="sm" icon="check" disabled={savingSched} onClick={saveSchedule}>{savingSched ? (isAr ? 'جارٍ…' : 'Saving…') : (isAr ? 'حفظ الجدول' : 'Save schedule')}</Btn>
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
