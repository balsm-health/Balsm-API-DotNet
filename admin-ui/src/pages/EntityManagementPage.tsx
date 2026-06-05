import { useState } from 'react';
import { Icon, Btn, IconBtn, Pill, Card, Switch, Field, TextInput } from '../components/atoms';
import type { Dir, Entity, Branch } from '../data';
import { ENTITIES } from '../data';

const EMPTY_BRANCH: Omit<Branch, 'id' | 'active'> = { name: '', code: '', address: '', phone: '' };

function BranchDrawer({ dir, entity, initial, onClose, onSave }: {
  dir: Dir;
  entity: Entity;
  initial: Partial<Branch>;
  onClose: () => void;
  onSave: (b: Branch) => void;
}) {
  const isAr = dir === 'rtl';
  const editing = !!initial.id;
  const [form, setForm] = useState<Omit<Branch, 'id' | 'active'> & { id?: string; active?: boolean }>({ ...EMPTY_BRANCH, ...initial });
  const [touched, setTouched] = useState(false);
  const set = (k: keyof typeof form, v: string) => setForm(f => ({ ...f, [k]: v }));
  const codeOk = /^BR-\d{3,}$/i.test((form.code ?? '').trim());
  const nameOk = (form.name ?? '').trim().length >= 2;
  const valid = nameOk && codeOk && (form.address ?? '').trim().length >= 3;

  const save = () => {
    setTouched(true);
    if (valid) onSave({ ...form, id: form.id ?? `br-${Date.now()}`, active: form.active ?? true, name: form.name.trim(), code: form.code.trim().toUpperCase() });
  };

  return (
    <div className="scrim" onClick={onClose} style={{ justifyContent: dir === 'rtl' ? 'flex-start' : 'flex-end', padding: 0, alignItems: 'stretch' }}>
      <div className="drawer" onClick={e => e.stopPropagation()}>
        <header className="drawer-head">
          <div>
            <span className="eyebrow" style={{ color: 'var(--petal-emerald)' }}>{entity.name}</span>
            <h2>{editing ? (isAr ? 'تعديل الفرع' : 'Edit branch') : (isAr ? 'فرع جديد' : 'New branch')}</h2>
          </div>
          <IconBtn icon="x" title="Close" onClick={onClose} />
        </header>
        <div className="drawer-body">
          <Field label={isAr ? 'اسم الفرع' : 'Branch name'} error={touched && !nameOk ? (isAr ? 'مطلوب' : 'Required') : undefined}>
            <TextInput value={form.name} onChange={v => set('name', v)} placeholder={isAr ? 'اسم الفرع' : 'Branch name'} autoFocus />
          </Field>
          <div className="field-row">
            <Field label={isAr ? 'الرمز' : 'Code'} hint="BR-###" error={touched && !codeOk ? (isAr ? 'صيغة غير صحيحة' : 'Use BR-###') : undefined}>
              <TextInput value={form.code} onChange={v => set('code', v)} placeholder="BR-104" mono />
            </Field>
            <Field label={isAr ? 'الهاتف' : 'Phone'}>
              <TextInput value={form.phone} onChange={v => set('phone', v)} placeholder="+20 2 …" mono />
            </Field>
          </div>
          <Field label={isAr ? 'العنوان' : 'Address'} error={touched && (form.address ?? '').trim().length < 3 ? (isAr ? 'مطلوب' : 'Required') : undefined}>
            <TextInput value={form.address} onChange={v => set('address', v)} placeholder={isAr ? '26 يوليو، القاهرة' : '26 July St, Cairo'} />
          </Field>
          <Field label={isAr ? 'الكيان الأصل' : 'Parent entity'}>
            <span className="static-field"><Icon name="building-2" size={15} /> {entity.name}</span>
          </Field>
          <div className="callout">
            <span className="ic"><Icon name="scroll-text" size={16} /></span>
            <div>{isAr ? 'سيُسجَّل هذا التغيير تلقائياً في سجل التدقيق.' : 'This change is recorded automatically in the audit log.'}</div>
          </div>
        </div>
        <footer className="drawer-foot">
          <Btn variant="ghost" onClick={onClose}>{isAr ? 'إلغاء' : 'Cancel'}</Btn>
          <Btn variant="primary" icon="check" disabled={touched && !valid} onClick={save}>{editing ? (isAr ? 'حفظ التغييرات' : 'Save changes') : (isAr ? 'إنشاء الفرع' : 'Create branch')}</Btn>
        </footer>
      </div>
    </div>
  );
}

function DeactivateModal({ dir, branch, reactivate, onClose, onConfirm }: {
  dir: Dir;
  branch: Branch;
  reactivate: boolean;
  onClose: () => void;
  onConfirm: () => void;
}) {
  const isAr = dir === 'rtl';
  return (
    <div className="scrim" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()}>
        <div className="modal-head">
          <span className={`mh-ic ${reactivate ? 'info' : 'violet'}`}><Icon name={reactivate ? 'rotate-ccw' : 'archive'} size={22} /></span>
          <div>
            <h2>{reactivate ? (isAr ? 'إعادة تفعيل الفرع؟' : 'Reactivate branch?') : (isAr ? 'إلغاء تفعيل الفرع؟' : 'Deactivate branch?')}</h2>
            <p>{branch.name} · <span className="mono">{branch.code}</span></p>
          </div>
        </div>
        <div className="modal-body">
          <div className={`callout ${reactivate ? '' : 'violet'}`}>
            <span className="ic"><Icon name="info" size={16} /></span>
            <div>{reactivate
              ? (isAr ? 'سيعود الفرع للظهور والعمل فوراً.' : 'The branch becomes visible and active again immediately.')
              : (isAr ? 'إلغاء التفعيل حذف ناعم — يبقى الفرع محفوظاً ومخفياً ويمكن استعادته في أي وقت. لا يُحذف نهائياً.' : 'Deactivating is a soft-delete — the branch is kept and hidden, and can be reactivated any time. Nothing is permanently removed.')}</div>
          </div>
        </div>
        <div className="modal-foot">
          <Btn variant="ghost" onClick={onClose}>{isAr ? 'إلغاء' : 'Cancel'}</Btn>
          <Btn variant={reactivate ? 'primary' : 'violet'} icon={reactivate ? 'rotate-ccw' : 'archive'} onClick={onConfirm}>
            {reactivate ? (isAr ? 'إعادة التفعيل' : 'Reactivate') : (isAr ? 'إلغاء التفعيل' : 'Deactivate')}
          </Btn>
        </div>
      </div>
    </div>
  );
}

interface EntityManagementPageProps {
  dir?: Dir;
  readOnly?: boolean;
  onAudit?: (msg: string, icon: string) => void;
}

export function EntityManagementPage({ dir = 'ltr', readOnly = false, onAudit }: EntityManagementPageProps) {
  const isAr = dir === 'rtl';
  const [entities, setEntities] = useState<Entity[]>(() => JSON.parse(JSON.stringify(ENTITIES)));
  const [selId, setSelId] = useState(ENTITIES[0].id);
  const [showInactive, setShowInactive] = useState(false);
  const [drawer, setDrawer] = useState<{ initial: Partial<Branch> } | null>(null);
  const [confirm, setConfirm] = useState<{ branch: Branch; reactivate: boolean } | null>(null);

  const sel = entities.find(e => e.id === selId) || entities[0];
  const branches = showInactive ? sel.branches : sel.branches.filter(b => b.active);
  const activeCount = sel.branches.filter(b => b.active).length;
  const inactiveCount = sel.branches.length - activeCount;

  const mutate = (fn: (e: Entity) => void) => setEntities(prev => {
    const next: Entity[] = JSON.parse(JSON.stringify(prev));
    const target = next.find(e => e.id === selId);
    if (target) fn(target);
    return next;
  });

  const saveBranch = (b: Branch) => {
    const editing = sel.branches.some(x => x.id === b.id);
    mutate(e => {
      if (editing) {
        const i = e.branches.findIndex(x => x.id === b.id);
        e.branches[i] = { ...e.branches[i], ...b };
      } else {
        e.branches.push({ ...b, id: 'br-' + Date.now(), active: true });
      }
    });
    setDrawer(null);
    onAudit?.(editing ? `Branch updated · ${b.name}` : `Branch created · ${b.name}`, 'building-2');
  };

  const toggleActive = (branch: Branch) => {
    mutate(e => { const x = e.branches.find(x => x.id === branch.id); if (x) x.active = !x.active; });
    onAudit?.(branch.active ? `Branch deactivated · ${branch.name}` : `Branch reactivated · ${branch.name}`, branch.active ? 'archive' : 'rotate-ccw');
    setConfirm(null);
  };

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'البيانات والأمان' : 'Data & safety'}</span>
          <h1>{isAr ? 'الكيانات والفروع' : 'Entities & branches'}</h1>
          <div className="sub">{isAr ? 'أنشئ وحرّر الكيانات وفروعها. الإلغاء حذف ناعم قابل للاستعادة.' : 'Create and edit organisations and their branches. Deactivation is a recoverable soft-delete.'}</div>
        </div>
        <div className="actions">
          <Btn variant="secondary" icon="plus" disabled={readOnly}>{isAr ? 'كيان جديد' : 'New entity'}</Btn>
          <Btn variant="primary" icon="plus" disabled={readOnly} onClick={() => setDrawer({ initial: { ...EMPTY_BRANCH } })}>{isAr ? 'فرع جديد' : 'New branch'}</Btn>
        </div>
      </div>

      <div className="two-col" style={{ gridTemplateColumns: '320px 1fr' }}>
        {/* master list */}
        <Card title={isAr ? 'الكيانات' : 'Entities'} icon="building-2">
          <div className="card-body flush">
            <div className="list-rows" style={{ padding: '4px 0' }}>
              {entities.map(e => (
                <button key={e.id} className={`entity-row ${e.id === selId ? 'sel' : ''}`} onClick={() => setSelId(e.id)}>
                  <span className="lr-ic"><Icon name="building-2" size={17} /></span>
                  <div className="lr-text">
                    <div className="t">{isAr ? e.nameAr : e.name}</div>
                    <div className="s">{e.type} · {e.branches.filter(b => b.active).length} {isAr ? 'فرع' : 'branches'}</div>
                  </div>
                  <Icon name={dir === 'rtl' ? 'chevron-left' : 'chevron-right'} size={16} />
                </button>
              ))}
            </div>
          </div>
        </Card>

        {/* branch table */}
        <Card
          title={`${isAr ? sel.nameAr : sel.name} · ${isAr ? 'الفروع' : 'branches'}`}
          icon="git-branch"
          actions={
            <label className="meta" style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: 'var(--fg3)', cursor: 'pointer' }}>
              <Switch on={showInactive} onChange={setShowInactive} disabled={inactiveCount === 0} />
              {isAr ? 'إظهار غير النشطة' : 'Show inactive'} {inactiveCount > 0 && <Pill tone="neutral" dot={false}>{inactiveCount}</Pill>}
            </label>
          }>
          <div className="card-body flush">
            {branches.length === 0 ? (
              <div className="empty">
                <span className="e-ic"><Icon name="git-branch" size={26} /></span>
                <h4>{isAr ? 'لا توجد فروع نشطة' : 'No active branches'}</h4>
                <p>{isAr ? 'فعّل عرض غير النشطة أو أنشئ فرعاً جديداً.' : 'Toggle "Show inactive", or create a new branch.'}</p>
              </div>
            ) : (
              <table className="balsm">
                <thead>
                  <tr>
                    <th>{isAr ? 'الفرع' : 'Branch'}</th>
                    <th>{isAr ? 'العنوان' : 'Address'}</th>
                    <th>{isAr ? 'الحالة' : 'Status'}</th>
                    <th className="right">{isAr ? 'إجراءات' : 'Actions'}</th>
                  </tr>
                </thead>
                <tbody>
                  {branches.map(b => (
                    <tr key={b.id} className={b.active ? '' : 'inactive'}>
                      <td>
                        <div className="name">{b.name}</div>
                        <div className="submeta mono">{b.code} · {b.phone}</div>
                      </td>
                      <td style={{ color: 'var(--fg2)' }}>{b.address}</td>
                      <td>{b.active ? <Pill tone="success" dot>{isAr ? 'نشط' : 'Active'}</Pill> : <Pill tone="neutral" dot>{isAr ? 'غير نشط' : 'Inactive'}</Pill>}</td>
                      <td className="right">
                        <div className="row-actions" style={{ justifyContent: dir === 'rtl' ? 'flex-start' : 'flex-end' }}>
                          <button title={isAr ? 'تعديل' : 'Edit'} disabled={readOnly} onClick={() => setDrawer({ initial: b })}><Icon name="pencil" size={15} /></button>
                          {b.active
                            ? <button className="danger" title={isAr ? 'إلغاء التفعيل' : 'Deactivate'} disabled={readOnly} onClick={() => setConfirm({ branch: b, reactivate: false })}><Icon name="archive" size={15} /></button>
                            : <button title={isAr ? 'إعادة التفعيل' : 'Reactivate'} disabled={readOnly} onClick={() => setConfirm({ branch: b, reactivate: true })}><Icon name="rotate-ccw" size={15} /></button>}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </Card>
      </div>

      {drawer && <BranchDrawer dir={dir} entity={sel} initial={drawer.initial} onClose={() => setDrawer(null)} onSave={saveBranch} />}
      {confirm && <DeactivateModal dir={dir} branch={confirm.branch} reactivate={confirm.reactivate} onClose={() => setConfirm(null)} onConfirm={() => toggleActive(confirm.branch)} />}
    </div>
  );
}
