import { useState } from 'react';
import React from 'react';
import { Icon, Btn, Pill, Card, IconBtn, Avatar, Segmented } from '../components/atoms';
import type { Dir } from '../data';
import { AUDIT, MODULE_COLORS, ARCHIVES } from '../data';

export function AuditPage({ dir = 'ltr' }: { dir?: Dir }) {
  const isAr = dir === 'rtl';
  const modules = ['all', 'Server', 'Entity', 'Backup', 'Identity', 'Audit'];
  const [filter, setFilter] = useState('all');
  const [expanded, setExpanded] = useState<string | null>(null);
  const [retention, setRetention] = useState('2y');

  const counts = AUDIT.reduce<Record<string, number>>((a, r) => { a[r.module] = (a[r.module] || 0) + 1; return a; }, {});
  const rows = filter === 'all' ? AUDIT : AUDIT.filter(r => r.module === filter);

  const verbIc = (v: string) => v === 'auth' ? 'key-round' : v === 'delete' ? 'archive' : v === 'create' ? 'plus' : 'pencil';

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'البيانات والأمان' : 'Data & safety'}</span>
          <h1>{isAr ? 'سجل التدقيق' : 'Audit log'}</h1>
          <div className="sub">{isAr ? 'جدول SQLite غير قابل للتعديل (append-only). كل عملية كتابة وتغيير مصادقة تُسجَّل تلقائياً.' : 'Append-only SQLite table. Every write and auth-state change is recorded automatically by the EF Core interceptor.'}</div>
        </div>
        <div className="actions">
          <Btn variant="secondary" icon="download">{isAr ? 'تصدير JSONL' : 'Export JSONL'}</Btn>
        </div>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
        <div className="chips">
          {modules.map(m => (
            <button key={m} className={`chip-filter ${filter === m ? 'active' : ''}`} onClick={() => setFilter(m)}>
              {isAr ? ({ Server: 'الخادم', Entity: 'الكيان', Backup: 'النسخ', Identity: 'الهوية', Audit: 'التدقيق', all: 'الكل' } as Record<string, string>)[m] || m : m}
              {' '}<span className="cnt">{m === 'all' ? AUDIT.length : (counts[m] || 0)}</span>
            </button>
          ))}
        </div>
        <span className="meta" style={{ fontSize: 12, color: 'var(--fg3)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="shield-check" size={14} /> {isAr ? 'الاحتفاظ: سنتان' : 'Retention: 2 years'}
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
                {rows.map(r => (
                  <React.Fragment key={r.id}>
                    <tr onClick={() => setExpanded(expanded === r.id ? null : r.id)} style={{ cursor: 'pointer' }}>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                          <Avatar initials={r.initials} size="sm" tone={r.actor === 'system' ? 'aqua' : 'violet'} />
                          <div>
                            <div className="name" style={{ fontSize: 13 }}>{r.actor}</div>
                            <div className="submeta mono">{r.ip}</div>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <Icon name={verbIc(r.verb)} size={15} style={{ color: 'var(--fg3)' }} />
                          <span style={{ fontWeight: 600, color: r.tone === 'danger' ? 'var(--balsm-danger)' : 'var(--balsm-ink-900)' }}>{r.action}</span>
                        </div>
                      </td>
                      <td><Pill tone={(MODULE_COLORS[r.module as keyof typeof MODULE_COLORS] as 'info' | 'aqua' | 'warn' | 'violet' | 'neutral') || 'neutral'} dot={false}>{r.module}</Pill></td>
                      <td className="mono" style={{ fontSize: 12, color: 'var(--fg2)' }}>{r.target}</td>
                      <td className="right num" style={{ fontSize: 12, color: 'var(--fg3)' }}>{r.time}</td>
                    </tr>
                    {expanded === r.id && (
                      <tr><td colSpan={5} style={{ background: 'var(--balsm-ink-50)', padding: '14px 18px' }}>
                        <div className="mono" style={{ fontSize: 12, color: 'var(--balsm-ink-700)', lineHeight: 1.8, whiteSpace: 'pre-wrap' }}>
{`{ "actor": "${r.actor}", "action": "${r.action}", "verb": "${r.verb}",
  "module": "${r.module}", "target": "${r.target}",
  "timestamp": "${r.time}Z", "source": "${r.ip}", "correlationId": "c-${r.id}f9a2" }`}
                        </div>
                      </td></tr>
                    )}
                  </React.Fragment>
                ))}
              </tbody>
            </table>
          </div>
        </Card>

        <div className="section-stack">
          <Card title={isAr ? 'الاحتفاظ' : 'Retention'} icon="timer-reset">
            <div className="field">
              <label>{isAr ? 'مدة الاحتفاظ' : 'Keep entries for'}</label>
              <Segmented value={retention} onChange={setRetention} options={[{value:'90d',label:'90d'},{value:'1y',label:'1y'},{value:'2y',label:'2y'},{value:'5y',label:'5y'}]} />
            </div>
            <div className="callout" style={{ marginTop: 16 }}>
              <span className="ic"><Icon name="info" size={16} /></span>
              <div>{isAr ? 'قبل الحذف تُصدَّر الصفوف المنتهية إلى أرشيف JSONL مؤرّخ في مجلد النسخ — لا تُحذف الأرشيفات تلقائياً.' : 'Before deletion, expired rows are exported to a dated JSONL archive in the backup directory. Archives are never auto-deleted.'}</div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
              <Btn variant="secondary" size="sm" icon="play">{isAr ? 'تشغيل التقليم الآن' : 'Run prune now'}</Btn>
            </div>
          </Card>

          <Card title={isAr ? 'الأرشيفات' : 'JSONL archives'} icon="file-archive">
            <div className="list-rows" style={{ marginTop: -6 }}>
              {ARCHIVES.map(a => (
                <div className="list-row" key={a.name}>
                  <span className="lr-ic"><Icon name="file-json" size={17} /></span>
                  <div className="lr-text">
                    <div className="t mono" style={{ fontSize: 12.5 }}>{a.name}</div>
                    <div className="s">{a.rows} {isAr ? 'صف' : 'rows'} · {a.size} · {a.date}</div>
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
