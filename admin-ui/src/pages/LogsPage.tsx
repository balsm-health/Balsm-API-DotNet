import { useState, useEffect, useCallback } from 'react';
import { Icon, Btn, Card, Segmented } from '../components/atoms';
import type { Dir } from '../data';
import { formatBytesStr, formatTimestamp } from '../data';
import { api } from '../api';
import type { LogFileDto } from '../api';

export function LogsPage({ dir = 'ltr' }: { dir?: Dir }) {
  const isAr = dir === 'rtl';
  const [files, setFiles] = useState<LogFileDto[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [lines, setLines] = useState(200);
  const [text, setText] = useState('');
  const [loading, setLoading] = useState(false);

  const loadFiles = useCallback(() => {
    api.getLogFiles()
      .then(r => {
        setFiles(r.items);
        setSelected(prev => prev ?? r.items[0]?.name ?? null);
      })
      .catch(() => setFiles([]));
  }, []);

  const loadTail = useCallback(() => {
    if (!selected) return;
    setLoading(true);
    api.tailLog(selected, lines)
      .then(setText)
      .catch(() => setText(isAr ? 'تعذّر تحميل السجل.' : 'Failed to load log.'))
      .finally(() => setLoading(false));
  }, [selected, lines, isAr]);

  useEffect(() => { loadFiles(); }, [loadFiles]);
  useEffect(() => { loadTail(); }, [loadTail]);

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'النظام' : 'System'}</span>
          <h1>{isAr ? 'السجلات' : 'Logs'}</h1>
          <div className="sub">{isAr ? 'سجلات Serilog اليومية المحفوظة محلياً في var/logs. تُعرض هنا ويمكن تنزيلها — وتُرسَل الأخطاء أيضاً إلى Sentry.' : 'Daily Serilog files stored locally in var/logs. View and download them here; errors are also forwarded to Sentry.'}</div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <Btn variant="secondary" size="sm" icon="refresh-cw" onClick={() => { loadFiles(); loadTail(); }}>{isAr ? 'تحديث' : 'Refresh'}</Btn>
          {selected && <Btn variant="secondary" size="sm" icon="download" onClick={() => api.downloadLog(selected).catch(() => {})}>{isAr ? 'تنزيل' : 'Download'}</Btn>}
        </div>
      </div>

      <div className="two-col">
        <div className="section-stack">
          <Card title={isAr ? 'ملفات السجل' : 'Log files'} icon="files">
            <div className="list-rows" style={{ marginTop: -6 }}>
              {files.length === 0 && <div className="empty-row" style={{ padding: '14px 4px', color: 'var(--fg3)', fontSize: 13 }}>{isAr ? 'لا توجد ملفات سجل بعد' : 'No log files yet'}</div>}
              {files.map(f => (
                <div
                  key={f.name}
                  className={`list-row ${selected === f.name ? 'active' : ''}`}
                  style={{ cursor: 'pointer', background: selected === f.name ? 'var(--balsm-ink-50)' : undefined }}
                  onClick={() => setSelected(f.name)}
                >
                  <span className="lr-ic"><Icon name="file-text" size={17} /></span>
                  <div className="lr-text">
                    <div className="t mono" style={{ fontSize: 12.5 }}>{f.name}</div>
                    <div className="s">{formatBytesStr(f.sizeBytes)} · {formatTimestamp(f.lastModified).split(' ')[0]}</div>
                  </div>
                </div>
              ))}
            </div>
          </Card>
        </div>

        <Card>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px', borderBottom: '1px solid var(--balsm-ink-100)' }}>
            <span className="mono" style={{ fontSize: 12.5, color: 'var(--fg2)' }}>{selected ?? '—'}</span>
            <Segmented
              value={String(lines)}
              onChange={v => setLines(Number(v))}
              options={[{ value: '100', label: '100' }, { value: '200', label: '200' }, { value: '500', label: '500' }, { value: '1000', label: '1000' }]}
            />
          </div>
          <div className="card-body flush">
            <pre
              className="mono"
              style={{ margin: 0, padding: '14px 16px', fontSize: 11.5, lineHeight: 1.6, color: 'var(--balsm-ink-700)', whiteSpace: 'pre-wrap', wordBreak: 'break-word', maxHeight: '62vh', overflow: 'auto', background: 'var(--balsm-ink-50)' }}
            >
              {loading ? (isAr ? 'جارٍ التحميل…' : 'Loading…') : (text || (isAr ? 'لا يوجد محتوى.' : 'No content.'))}
            </pre>
          </div>
        </Card>
      </div>
    </div>
  );
}
