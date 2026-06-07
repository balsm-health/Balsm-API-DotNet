import { useState, useEffect } from 'react';
import React from 'react';
import { Icon, Btn, Pill, Card, IconBtn, Avatar, Segmented } from '../components/atoms';
import type { Dir } from '../data';
import { MODULE_COLORS, formatBytesStr, formatTimestamp } from '../data';
import { api } from '../api';
import type { AuditLogDto, AuditArchiveDto } from '../api';

const MODULES = ['all', 'Server', 'Mode', 'Backup', 'Identity', 'Audit', 'Entity'];

function verbOf(action: unknown): string {
  const a = String(action ?? '').toLowerCase();
  if (a.includes('login') || a.includes('auth') || a.includes('recovery') || a.includes('lock')) return 'auth';
  if (a.includes('deactiv') || a.includes('delete') || a.includes('prune') || a.includes('remov')) return 'delete';
  if (a.includes('creat') || a.includes('taken') || a.includes('add')) return 'create';
  return 'update';
}

export function AuditPage({ dir = 'ltr' }: { dir?: Dir }) {
  const isAr = dir === 'rtl';
  const [filter, setFilter] = useState('all');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [logs, setLogs] = useState<AuditLogDto[]>([]);
  const [archives, setArchives] = useState<AuditArchiveDto[]>([]);
  const [retentionYears, setRetentionYears] = useState(2);
  const [retentionCron, setRetentionCron] = useState('0 3 * * *');
  const [savingRetention, setSavingRetention] = useState(false);

  useEffect(() => {
    api.getAuditLogs({ pageSize: 200 }).then(r => setLogs(r.items)).catch(() => setLogs([]));
    api.getAuditArchives(1, 50).then(r => setArchives(r.items)).catch(() => setArchives([]));
    api.getAuditRetention().then(r => { setRetentionYears(r.retentionYears); setRetentionCron(r.cron); }).catch(() => {});
  }, []);

  const counts = logs.reduce<Record<string, number>>((a, r) => { a[r.module] = (a[r.module] || 0) + 1; return a; }, {});
  const rows = filter === 'all' ? logs : logs.filter(r => r.module === filter);

  const verbIc = (v: string) => v === 'auth' ? 'key-round' : v === 'delete' ? 'archive' : v === 'create' ? 'plus' : 'pencil';

  const saveRetention = async () => {
    setSavingRetention(true);
    await api.updateAuditRetention(retentionCron, retentionYears).catch(() => null);
    setSavingRetention(false);
  };

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'البيانات والأمان' : 'Data & safety'}</span>
          <h1>{isAr ? 'سجل التدقيق' : 'Audit log'}</h1>
          <div className="sub">{isAr ? 'جدول SQLite غير قابل للتعديل (append-only). كل عملية كتابة وتغيير مصادقة تُسجَّل تلقائياً.' : 'Append-only SQLite table. Every write and auth-state change is recorded automatically by the EF Core interceptor.'}</div>
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
        <div className="chips">
          {MODULES.map(m => (
            <button key={m} className={`chip-filter ${filter === m ? 'active' : ''}`} onClick={() => setFilter(m)}>
              {isAr ? ({ Server: 'الخادم', Mode: 'الوضع', Backup: 'النسخ', Identity: 'الهوية', Audit: 'التدقيق', Entity: 'الكيان', all: 'الكل' } as Record<string, string>)[m] || m : m}
              {' '}<span className="cnt">{m === 'all' ? logs.length : (counts[m] || 0)}</span>
            </button>
          ))}
        </div>
        <span className="meta" style={{ fontSize: 12, color: 'var(--fg3)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="shield-check" size={14} /> {isAr ? `الاحتفاظ: ${retentionYears} سنة` : `Retention: ${retentionYears} year${retentionYears === 1 ? '' : 's'}`}
        </span>
      </div>

      <div className="two-col">
        <Card>
          <div className="card-body flush">
            <table className="balsm">
              <thead>
                <tr>
                  <th>{isAr ? 'الفاعل' : 'Actor'}</th>
                  <th>{isAr ? 'الإجراء' : 'Action'}</th>
                  <th>{isAr ? 'الوحدة' : 'Module'}</th>
                  <th>{isAr ? 'الهدف' : 'Target'}</th>
                  <th className="right">{isAr ? 'التوقيت (UTC)' : 'Timestamp (UTC)'}</th>
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr><td colSpan={5} style={{ padding: '22px 18px', color: 'var(--fg3)', textAlign: 'center' }}>{isAr ? 'لا توجد سجلات.' : 'No audit entries.'}</td></tr>
                )}
                {rows.map(r => {
                  const verb = verbOf(r.action);
                  const danger = String(r.action ?? '').toLowerCase().includes('fail') || String(r.action ?? '').toLowerCase().includes('lock');
                  const initials = (r.actor || '??').slice(0, 2).toUpperCase();
                  const ts = formatTimestamp(r.occurredAt);
                  return (
                    <React.Fragment key={r.id}>
                      <tr onClick={() => setExpanded(expanded === r.id ? null : r.id)} style={{ cursor: 'pointer' }}>
                        <td>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                            <Avatar initials={initials} size="sm" tone={r.actor === 'system' ? 'aqua' : 'violet'} />
                            <div>
                              <div className="name" style={{ fontSize: 13 }}>{r.actor}</div>
                              <div className="submeta mono">{r.sourceIp ?? 'system'}</div>
                            </div>
                          </div>
                        </td>
                        <td>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <Icon name={verbIc(verb)} size={15} style={{ color: 'var(--fg3)' }} />
                            <span style={{ fontWeight: 600, color: danger ? 'var(--balsm-danger)' : 'var(--balsm-ink-900)' }}>{r.action}</span>
                          </div>
                        </td>
                        <td><Pill tone={(MODULE_COLORS[r.module] as 'info' | 'aqua' | 'warn' | 'violet' | 'neutral') || 'neutral'} dot={false}>{r.module}</Pill></td>
                        <td className="mono" style={{ fontSize: 12, color: 'var(--fg2)' }}>{r.targetId ?? r.targetType ?? '—'}</td>
                        <td className="right num" style={{ fontSize: 12, color: 'var(--fg3)' }}>{ts}</td>
                      </tr>
                      {expanded === r.id && (
                        <tr><td colSpan={5} style={{ background: 'var(--balsm-ink-50)', padding: '14px 18px' }}>
                          <div className="mono" style={{ fontSize: 12, color: 'var(--balsm-ink-700)', lineHeight: 1.8, whiteSpace: 'pre-wrap' }}>
{`{ "actor": "${r.actor}", "action": "${r.action}",
  "module": "${r.module}", "targetType": "${r.targetType ?? ''}", "targetId": "${r.targetId ?? ''}",
  "occurredAt": "${r.occurredAt}", "sourceIp": "${r.sourceIp ?? ''}", "correlationId": "${r.correlationId ?? ''}",
  "details": ${r.detailsJson ?? 'null'} }`}
                          </div>
                        </td></tr>
                      )}
                    </React.Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>

        <div className="section-stack">
          <Card title={isAr ? 'الاحتفاظ' : 'Retention'} icon="timer-reset">
            <div className="field">
              <label>{isAr ? 'مدة الاحتفاظ (سنوات)' : 'Keep entries for'}</label>
              <Segmented value={String(retentionYears)} onChange={v => setRetentionYears(Number(v))} options={[{ value: '1', label: '1y' }, { value: '2', label: '2y' }, { value: '5', label: '5y' }]} />
            </div>
            <div className="callout" style={{ marginTop: 16 }}>
              <span className="ic"><Icon name="info" size={16} /></span>
              <div>{isAr ? 'قبل الحذف تُصدَّر الصفوف المنتهية إلى أرشيف JSONL مؤرّخ في مجلد النسخ — لا تُحذف الأرشيفات تلقائياً.' : 'Before deletion, expired rows are exported to a dated JSONL archive in the backup directory. Archives are never auto-deleted.'}</div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
              <Btn variant="secondary" size="sm" icon="check" disabled={savingRetention} onClick={saveRetention}>{savingRetention ? (isAr ? 'جارٍ…' : 'Saving…') : (isAr ? 'حفظ' : 'Save')}</Btn>
            </div>
          </Card>

          <Card title={isAr ? 'الأرشيفات' : 'JSONL archives'} icon="file-archive">
            <div className="list-rows" style={{ marginTop: -6 }}>
              {archives.length === 0 && <div className="empty-row" style={{ padding: '14px 4px', color: 'var(--fg3)', fontSize: 13 }}>{isAr ? 'لا توجد أرشيفات بعد' : 'No archives yet'}</div>}
              {archives.map(a => (
                <div className="list-row" key={a.id}>
                  <span className="lr-ic"><Icon name="file-json" size={17} /></span>
                  <div className="lr-text">
                    <div className="t mono" style={{ fontSize: 12.5 }}>{a.filename}</div>
                    <div className="s">{a.rowCount} {isAr ? 'صف' : 'rows'} · {formatBytesStr(a.sizeBytes)} · {formatTimestamp(a.archivedAt).split(' ')[0]}</div>
                  </div>
                  <IconBtn icon="download" size={30} title="Download" />
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}
