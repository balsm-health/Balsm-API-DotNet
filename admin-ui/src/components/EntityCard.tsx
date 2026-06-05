import { useState } from 'react'
import { api, EntityDto, UpdateEntityRequest } from '../api'
import { BranchListCard } from './BranchListCard'

interface Props {
  entity: EntityDto
  onUpdated: () => void
  onDeactivated: () => void
  onReactivated: () => void
}

export function EntityCard({ entity, onUpdated, onDeactivated, onReactivated }: Props) {
  const [editing, setEditing] = useState(false)
  const [showBranches, setShowBranches] = useState(false)
  const [form, setForm] = useState<UpdateEntityRequest>({
    name: entity.name,
    registrationNumber: entity.registrationNumber ?? '',
  })

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault()
    const res = await api.updateEntity(entity.id, form)
    if (res.ok) {
      setEditing(false)
      onUpdated()
    } else {
      alert('Failed to update entity.')
    }
  }

  async function handleDeactivate() {
    if (!confirm(`Deactivate "${entity.name}"?`)) return
    const res = await api.deactivateEntity(entity.id)
    if (res.ok) onDeactivated()
    else alert('Failed to deactivate.')
  }

  async function handleReactivate() {
    const res = await api.reactivateEntity(entity.id)
    if (res.ok) onReactivated()
    else alert('Failed to reactivate.')
  }

  return (
    <div className={`card entity-card ${!entity.isActive ? 'card-inactive' : ''}`}>
      <div className="card-header">
        {editing ? (
          <form onSubmit={handleUpdate} className="inline-form">
            <input required value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
            <input placeholder="Registration number" value={form.registrationNumber ?? ''}
              onChange={e => setForm(f => ({ ...f, registrationNumber: e.target.value }))} />
            <button type="submit" className="btn btn-sm btn-primary">Save</button>
            <button type="button" className="btn btn-sm btn-outline" onClick={() => setEditing(false)}>Cancel</button>
          </form>
        ) : (
          <>
            <div>
              <h3 className="entity-name">{entity.name}</h3>
              <span className="entity-type">{entity.typeCode}</span>
              {entity.registrationNumber && (
                <span className="entity-reg">Reg: {entity.registrationNumber}</span>
              )}
              {!entity.isActive && <span className="badge badge-inactive">Inactive</span>}
            </div>
            <div className="item-actions">
              <button className="btn btn-sm btn-outline" onClick={() => setEditing(true)}>Edit</button>
              <button className="btn btn-sm btn-outline" onClick={() => setShowBranches(v => !v)}>
                {showBranches ? 'Hide Branches' : 'Branches'}
              </button>
              {entity.isActive
                ? <button className="btn btn-sm btn-danger" onClick={handleDeactivate}>Deactivate</button>
                : <button className="btn btn-sm btn-outline" onClick={handleReactivate}>Reactivate</button>
              }
            </div>
          </>
        )}
      </div>

      {showBranches && (
        <BranchListCard entityId={entity.id} entityName={entity.name} />
      )}
    </div>
  )
}
