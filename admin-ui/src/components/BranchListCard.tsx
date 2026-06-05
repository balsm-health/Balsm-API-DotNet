import { useEffect, useState } from 'react'
import { api, BranchDto, CreateBranchRequest, UpdateBranchRequest } from '../api'

interface Props {
  entityId: string
  entityName: string
}

export function BranchListCard({ entityId, entityName }: Props) {
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [includeInactive, setIncludeInactive] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)

  const [createForm, setCreateForm] = useState<CreateBranchRequest>({
    name: '', addressLine1: '', city: '', countryCode: 'SA',
  })
  const [editForm, setEditForm] = useState<UpdateBranchRequest>({
    name: '', addressLine1: '', city: '', countryCode: 'SA',
  })

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setBranches(await api.getBranches(entityId, includeInactive))
    } catch {
      setError('Failed to load branches.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [entityId, includeInactive]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    const res = await api.createBranch(entityId, createForm)
    if (res.ok) {
      setShowCreateForm(false)
      setCreateForm({ name: '', addressLine1: '', city: '', countryCode: 'SA' })
      load()
    } else {
      alert('Failed to create branch.')
    }
  }

  async function handleUpdate(id: string) {
    const res = await api.updateBranch(entityId, id, editForm)
    if (res.ok) {
      setEditingId(null)
      load()
    } else {
      alert('Failed to update branch.')
    }
  }

  async function handleDeactivate(id: string) {
    if (!confirm('Deactivate this branch?')) return
    const res = await api.deactivateBranch(entityId, id)
    if (res.ok) load()
    else alert('Failed to deactivate branch.')
  }

  async function handleReactivate(id: string) {
    const res = await api.reactivateBranch(entityId, id)
    if (res.ok) load()
    else alert('Failed to reactivate branch.')
  }

  return (
    <div className="card">
      <div className="card-header">
        <h3>Branches — {entityName}</h3>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <label style={{ fontSize: '0.8rem' }}>
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={e => setIncludeInactive(e.target.checked)}
              style={{ marginRight: '0.25rem' }}
            />
            Show inactive
          </label>
          <button className="btn btn-sm btn-primary" onClick={() => setShowCreateForm(v => !v)}>
            + Branch
          </button>
        </div>
      </div>

      {error && <p className="error-text">{error}</p>}

      {showCreateForm && (
        <form onSubmit={handleCreate} className="inline-form">
          <input required placeholder="Branch name" value={createForm.name}
            onChange={e => setCreateForm(f => ({ ...f, name: e.target.value }))} />
          <input placeholder="Address line 1" value={createForm.addressLine1 ?? ''}
            onChange={e => setCreateForm(f => ({ ...f, addressLine1: e.target.value }))} />
          <input placeholder="City" value={createForm.city ?? ''}
            onChange={e => setCreateForm(f => ({ ...f, city: e.target.value }))} />
          <input placeholder="Country code" maxLength={2} value={createForm.countryCode ?? ''}
            onChange={e => setCreateForm(f => ({ ...f, countryCode: e.target.value.toUpperCase() }))} />
          <button type="submit" className="btn btn-sm btn-primary">Create</button>
          <button type="button" className="btn btn-sm btn-outline" onClick={() => setShowCreateForm(false)}>Cancel</button>
        </form>
      )}

      {loading ? (
        <p className="muted-text">Loading...</p>
      ) : branches.length === 0 ? (
        <p className="muted-text">No branches found.</p>
      ) : (
        <ul className="item-list">
          {branches.map(b => (
            <li key={b.id} className={`item-row ${!b.isActive ? 'item-inactive' : ''}`}>
              {editingId === b.id ? (
                <div className="inline-form">
                  <input required value={editForm.name}
                    onChange={e => setEditForm(f => ({ ...f, name: e.target.value }))} />
                  <input placeholder="Address line 1" value={editForm.addressLine1 ?? ''}
                    onChange={e => setEditForm(f => ({ ...f, addressLine1: e.target.value }))} />
                  <input placeholder="City" value={editForm.city ?? ''}
                    onChange={e => setEditForm(f => ({ ...f, city: e.target.value }))} />
                  <button className="btn btn-sm btn-primary" onClick={() => handleUpdate(b.id)}>Save</button>
                  <button className="btn btn-sm btn-outline" onClick={() => setEditingId(null)}>Cancel</button>
                </div>
              ) : (
                <>
                  <span className="item-name">{b.name}</span>
                  {b.city && <span className="item-meta">{b.city}</span>}
                  {!b.isActive && <span className="badge badge-inactive">Inactive</span>}
                  <div className="item-actions">
                    <button className="btn btn-xs btn-outline" onClick={() => {
                      setEditForm({ name: b.name, addressLine1: b.addressLine1 ?? '', city: b.city ?? '', countryCode: b.countryCode ?? '' })
                      setEditingId(b.id)
                    }}>Edit</button>
                    {b.isActive
                      ? <button className="btn btn-xs btn-danger" onClick={() => handleDeactivate(b.id)}>Deactivate</button>
                      : <button className="btn btn-xs btn-outline" onClick={() => handleReactivate(b.id)}>Reactivate</button>
                    }
                  </div>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
