import { useEffect, useState } from 'react'
import { catalogApi, money } from './api.js'
import PreviewDialog from '../components/PreviewDialog.jsx'
import Icon from '../components/Icon.jsx'
import '../pages/catalog.css'

function Field({
  name,
  label,
  value,
  onChange,
  error,
  type = 'text',
  options,
  multiline,
  ...props
}) {
  const id = `catalog-${name.replaceAll('.', '-')}`
  const attributes = {
    id,
    name,
    value: value ?? '',
    onChange: (event) => onChange(event.target.value),
    'aria-invalid': Boolean(error),
    'aria-describedby': error ? `${id}-error` : undefined,
    required: true,
    ...props,
  }
  return (
    <div className="catalog-field">
      <label htmlFor={id}>{label}</label>
      {options ? (
        <select {...attributes}>
          {options.map(([value, text]) => (
            <option key={value} value={value}>
              {text}
            </option>
          ))}
        </select>
      ) : multiline ? (
        <textarea {...attributes} rows={3} />
      ) : (
        <input {...attributes} type={type} />
      )}
      {error && (
        <span id={`${id}-error`} className="catalog-field-error">
          {label}: {error}
        </span>
      )}
    </div>
  )
}

function EditError({ error, es, onReload }) {
  if (!error) return null
  const messages = es
    ? {
        VALIDATION_FAILED: 'Revisa los campos señalados y los límites de ocupación.',
        DUPLICATE_CATALOG_KEY: 'Ese identificador o número ya está en uso.',
        CATALOG_CHANGED: 'Otra sesión modificó este registro. Recarga antes de guardar.',
        INVALID_ROOM_TYPE: 'Selecciona un tipo de habitación válido.',
        ROOM_TYPE_IMMUTABLE: 'El tipo de una habitación física no se puede cambiar.',
        CSRF_INVALID: 'La sesión de seguridad cambió. Vuelve a intentarlo.',
        NETWORK: 'No pudimos completar la operación. Comprueba la conexión y reintenta.',
        REFERENCE_CHANGED: 'La referencia cambió. Recarga los datos.',
      }
    : {
        VALIDATION_FAILED: 'Check the highlighted fields and occupancy limits.',
        DUPLICATE_CATALOG_KEY: 'That identifier or room number is already in use.',
        CATALOG_CHANGED: 'Another session changed this record. Reload before saving.',
        INVALID_ROOM_TYPE: 'Select a valid room type.',
        ROOM_TYPE_IMMUTABLE: 'A physical room’s type cannot be changed.',
        CSRF_INVALID: 'The security session changed. Please try again.',
        NETWORK: 'Could not complete the operation. Check your connection and retry.',
        REFERENCE_CHANGED: 'The reference changed. Reload the data.',
      }
  return (
    <div className="catalog-error">
      <p role="alert">{messages[error.code] ?? messages.NETWORK}</p>
      {error.status === 409 && (
        <button type="button" className="text-button" onClick={onReload}>
          {es ? 'Descartar cambios y recargar' : 'Discard changes and reload'}
        </button>
      )}
    </div>
  )
}

function EditorFields({ draft, setDraft, error, es }) {
  return (name, labelEs, labelEn, props = {}) => (
    <Field
      name={name}
      label={es ? labelEs : labelEn}
      value={draft[name]}
      onChange={(value) => setDraft({ ...draft, [name]: value })}
      error={error?.fields?.[name] ? (es ? 'Valor no válido' : 'Invalid value') : null}
      {...props}
    />
  )
}

function TypeEditor({ initial, assets, es, language, onSave, busy, error, onReload }) {
  const [draft, setDraft] = useState(
    initial ?? {
      slug: '',
      nameEs: '',
      nameEn: '',
      descriptionEs: '',
      descriptionEn: '',
      category: 'room',
      beds: 'king',
      area: 57,
      riverView: false,
      capacity: 2,
      maxAdults: 2,
      maxChildren: 1,
      nightlyRate: 880,
      active: true,
      sortOrder: 99,
      revision: 0,
      images: [{ url: assets[0]?.url ?? '', altEs: '', altEn: '' }],
    },
  )
  const field = EditorFields({ draft, setDraft, error, es })
  const changeImage = (index, key, value) =>
    setDraft({
      ...draft,
      images: draft.images.map((image, i) => (i === index ? { ...image, [key]: value } : image)),
    })
  const moveImage = (index, offset) => {
    const images = [...draft.images]
    ;[images[index], images[index + offset]] = [images[index + offset], images[index]]
    setDraft({ ...draft, images })
  }
  return (
    <form
      className="catalog-form"
      aria-label={es ? 'Editar tipo de habitación' : 'Edit room type'}
      onSubmit={(event) => {
        event.preventDefault()
        const body = { ...draft }
        for (const key of [
          'area',
          'capacity',
          'maxAdults',
          'maxChildren',
          'nightlyRate',
          'sortOrder',
        ])
          body[key] = Number(body[key])
        onSave(body)
      }}
    >
      <fieldset disabled={busy}>
        <legend>{es ? 'Contenido bilingüe' : 'Bilingual content'}</legend>
        <div className="catalog-form-grid">
          {field('slug', 'Identificador', 'Identifier', {
            maxLength: 80,
            pattern: '[a-z0-9]+(-[a-z0-9]+)*',
          })}
          {field('category', 'Categoría', 'Category', {
            options: [
              ['room', es ? 'Habitación' : 'Room'],
              ['suite', 'Suite'],
            ],
          })}
          {field('nameEs', 'Nombre (ES)', 'Name (ES)', { maxLength: 180 })}
          {field('nameEn', 'Nombre (EN)', 'Name (EN)', { maxLength: 180 })}
          {field('descriptionEs', 'Descripción (ES)', 'Description (ES)', {
            multiline: true,
            maxLength: 4000,
          })}
          {field('descriptionEn', 'Descripción (EN)', 'Description (EN)', {
            multiline: true,
            maxLength: 4000,
          })}
          {field('beds', 'Camas', 'Beds', {
            options: [
              ['king', '1 King'],
              ['choice', es ? '1 King o 2 Twin' : '1 King or 2 Twins'],
            ],
          })}
          {field('area', 'Superficie (m²)', 'Area (m²)', { type: 'number', min: 1, max: 1000 })}
          {field('capacity', 'Capacidad total de demostración', 'Demonstration total capacity', {
            type: 'number',
            min: 1,
            max: 12,
          })}
          {field('maxAdults', 'Máximo de adultos', 'Maximum adults', {
            type: 'number',
            min: 1,
            max: Number(draft.capacity) || 12,
          })}
          {field('maxChildren', 'Máximo de niños', 'Maximum children', {
            type: 'number',
            min: 0,
            max: Math.max(0, Number(draft.capacity) - 1),
          })}
          {field(
            'nightlyRate',
            'Tarifa de demostración CNY / noche',
            'Demonstration rate CNY / night',
            { type: 'number', min: 0.01, max: 9999999.99, step: 0.01 },
          )}
          {field('sortOrder', 'Orden de presentación', 'Display order', {
            type: 'number',
            min: 0,
            max: 9999,
          })}
        </div>
        <div className="catalog-checks">
          <label>
            <input
              type="checkbox"
              checked={draft.active}
              onChange={(event) => setDraft({ ...draft, active: event.target.checked })}
            />
            {es ? 'Publicado en el catálogo' : 'Published in the catalogue'}
          </label>
          <label>
            <input
              type="checkbox"
              checked={draft.riverView}
              onChange={(event) => setDraft({ ...draft, riverView: event.target.checked })}
            />
            {es ? 'Vista al río' : 'River view'}
          </label>
        </div>
        <p className="catalog-hint">
          {es
            ? 'Niños: 0–17 años. Cada habitación requiere al menos un adulto. La tarifa y capacidad son ficticias.'
            : 'Children: ages 0–17. Each room needs at least one adult. Rates and capacities are fictional.'}
        </p>
      </fieldset>
      <fieldset disabled={busy}>
        <legend>{es ? 'Galería ordenada' : 'Ordered gallery'}</legend>
        <p className="catalog-hint">
          {es
            ? 'Selecciona fotografías locales acreditadas. El orden de la primera imagen define la portada de la tarjeta.'
            : 'Choose attributed local photographs. The first image becomes the card cover.'}
        </p>
        {draft.images.map((image, index) => (
          <div className="catalog-image-editor" key={index}>
            <img src={image.url} alt={es ? image.altEs : image.altEn} />
            <div>
              <Field
                name={`images.${index}.url`}
                label={`${es ? 'Imagen' : 'Image'} ${index + 1}`}
                value={image.url}
                options={assets.map((asset) => [asset.url, asset.url.split('/').pop()])}
                onChange={(value) => changeImage(index, 'url', value)}
                error={
                  error?.fields?.[`images.${index}.url`]
                    ? es
                      ? 'Imagen no válida'
                      : 'Invalid image'
                    : null
                }
              />
              <Field
                name={`images.${index}.altEs`}
                label={`${es ? 'Texto alternativo' : 'Alternative text'} (ES) ${index + 1}`}
                value={image.altEs}
                maxLength={250}
                onChange={(value) => changeImage(index, 'altEs', value)}
                error={
                  error?.fields?.[`images.${index}.altEs`]
                    ? es
                      ? 'Obligatorio'
                      : 'Required'
                    : null
                }
              />
              <Field
                name={`images.${index}.altEn`}
                label={`${es ? 'Texto alternativo' : 'Alternative text'} (EN) ${index + 1}`}
                value={image.altEn}
                maxLength={250}
                onChange={(value) => changeImage(index, 'altEn', value)}
                error={
                  error?.fields?.[`images.${index}.altEn`]
                    ? es
                      ? 'Obligatorio'
                      : 'Required'
                    : null
                }
              />
              <div className="catalog-image-actions">
                <button
                  type="button"
                  disabled={index === 0}
                  aria-label={`${es ? 'Subir imagen' : 'Move image up'} ${index + 1}`}
                  onClick={() => moveImage(index, -1)}
                >
                  ↑
                </button>
                <button
                  type="button"
                  disabled={index === draft.images.length - 1}
                  aria-label={`${es ? 'Bajar imagen' : 'Move image down'} ${index + 1}`}
                  onClick={() => moveImage(index, 1)}
                >
                  ↓
                </button>
                <button
                  type="button"
                  disabled={draft.images.length === 1}
                  onClick={() =>
                    setDraft({ ...draft, images: draft.images.filter((_, i) => i !== index) })
                  }
                >
                  {es ? 'Quitar imagen' : 'Remove image'} {index + 1}
                </button>
              </div>
              <small>
                Regent / IHG · Copyright ·{' '}
                <a
                  href={assets.find((asset) => asset.url === image.url)?.sourceUrl}
                  target="_blank"
                  rel="noreferrer"
                >
                  {es ? 'Procedencia' : 'Source'} ↗
                </a>
              </small>
            </div>
          </div>
        ))}
        <button
          className="button button-outline"
          type="button"
          disabled={draft.images.length >= 12 || draft.images.length >= assets.length}
          onClick={() =>
            setDraft({
              ...draft,
              images: [
                ...draft.images,
                {
                  url:
                    assets.find((asset) => !draft.images.some((image) => image.url === asset.url))
                      ?.url ?? assets[0].url,
                  altEs: '',
                  altEn: '',
                },
              ],
            })
          }
        >
          {es ? 'Añadir imagen' : 'Add image'}
        </button>
      </fieldset>
      <EditError error={error} es={es} onReload={onReload} />
      <button className="button button-cyan" disabled={busy} type="submit">
        {busy ? (es ? 'Guardando…' : 'Saving…') : es ? 'Guardar tipo' : 'Save room type'}
        <Icon name="arrow" />
      </button>
    </form>
  )
}

function HotelEditor({ hotel, es, busy, error, onReload, onSave }) {
  const [draft, setDraft] = useState(hotel)
  const field = EditorFields({ draft, setDraft, error, es })
  return (
    <form
      className="catalog-form"
      aria-label={es ? 'Editar hotel' : 'Edit hotel'}
      onSubmit={(event) => {
        event.preventDefault()
        onSave({ ...draft, latitude: Number(draft.latitude), longitude: Number(draft.longitude) })
      }}
    >
      <fieldset disabled={busy}>
        <legend>{es ? 'Hotel y ubicación' : 'Hotel & location'}</legend>
        <div className="catalog-form-grid">
          {field('name', 'Nombre del hotel', 'Hotel name', { maxLength: 180 })}
          {field('address', 'Dirección', 'Address', { maxLength: 500 })}
          {field('descriptionEs', 'Descripción del hotel (ES)', 'Hotel description (ES)', {
            multiline: true,
            maxLength: 4000,
          })}
          {field('descriptionEn', 'Descripción del hotel (EN)', 'Hotel description (EN)', {
            multiline: true,
            maxLength: 4000,
          })}
          {field('latitude', 'Latitud aproximada', 'Approximate latitude', {
            type: 'number',
            min: -90,
            max: 90,
            step: 0.000001,
          })}
          {field('longitude', 'Longitud aproximada', 'Approximate longitude', {
            type: 'number',
            min: -180,
            max: 180,
            step: 0.000001,
          })}
          {field('timeZone', 'Zona horaria', 'Time zone', { readOnly: true })}
        </div>
      </fieldset>
      <fieldset disabled={busy}>
        <legend>{es ? 'Servicios bilingües' : 'Bilingual services'}</legend>
        {draft.services.map((service, index) => (
          <div className="catalog-service-editor" key={index}>
            <Field
              name={`services.${index}.nameEs`}
              label={`${es ? 'Servicio' : 'Service'} ${index + 1} (ES)`}
              value={service.nameEs}
              error={
                error?.fields?.[`services.${index}.nameEs`]
                  ? es
                    ? 'Obligatorio'
                    : 'Required'
                  : null
              }
              maxLength={100}
              onChange={(value) =>
                setDraft({
                  ...draft,
                  services: draft.services.map((item, i) =>
                    i === index ? { ...item, nameEs: value } : item,
                  ),
                })
              }
            />
            <Field
              name={`services.${index}.nameEn`}
              label={`${es ? 'Servicio' : 'Service'} ${index + 1} (EN)`}
              value={service.nameEn}
              error={
                error?.fields?.[`services.${index}.nameEn`]
                  ? es
                    ? 'Obligatorio'
                    : 'Required'
                  : null
              }
              maxLength={100}
              onChange={(value) =>
                setDraft({
                  ...draft,
                  services: draft.services.map((item, i) =>
                    i === index ? { ...item, nameEn: value } : item,
                  ),
                })
              }
            />
            <button
              type="button"
              className="text-button"
              onClick={() =>
                setDraft({ ...draft, services: draft.services.filter((_, i) => i !== index) })
              }
            >
              {es ? 'Quitar servicio' : 'Remove service'} {index + 1}
            </button>
          </div>
        ))}
        <button
          type="button"
          className="button button-outline"
          disabled={draft.services.length >= 20}
          onClick={() =>
            setDraft({ ...draft, services: [...draft.services, { nameEs: '', nameEn: '' }] })
          }
        >
          {es ? 'Añadir servicio' : 'Add service'}
        </button>
      </fieldset>
      <EditError error={error} es={es} onReload={onReload} />
      <button type="submit" className="button button-cyan" disabled={busy}>
        {busy ? (es ? 'Guardando…' : 'Saving…') : es ? 'Guardar hotel' : 'Save hotel'}
      </button>
    </form>
  )
}

function RoomEditor({ initial, allTypes, es, busy, error, onReload, onSave }) {
  const [draft, setDraft] = useState(
    initial ?? { roomTypeId: allTypes[0]?.id ?? '', number: '', active: true, revision: 0 },
  )
  const field = EditorFields({ draft, setDraft, error, es })
  return (
    <form
      className="catalog-form"
      aria-label={es ? 'Editar habitación física' : 'Edit physical room'}
      onSubmit={(event) => {
        event.preventDefault()
        onSave(draft)
      }}
    >
      <fieldset disabled={busy}>
        <legend>{es ? 'Inventario de demostración' : 'Demonstration inventory'}</legend>
        <div className="catalog-form-grid">
          {field('number', 'Número de habitación', 'Room number', {
            maxLength: 12,
            pattern: '[A-Za-z0-9-]{1,12}',
          })}
          {field('roomTypeId', 'Tipo de habitación', 'Room type', {
            disabled: Boolean(initial),
            options: allTypes.map((type) => [
              type.id,
              (es ? type.nameEs : type.nameEn) +
                (type.active ? '' : es ? ' · sin publicar' : ' · unpublished'),
            ]),
          })}
        </div>
        <label className="catalog-checkbox">
          <input
            type="checkbox"
            checked={draft.active}
            onChange={(event) => setDraft({ ...draft, active: event.target.checked })}
          />
          {es ? 'Habitación operativa' : 'Operational room'}
        </label>
        <p className="catalog-hint">
          {es
            ? 'El número es único en el hotel. El tipo queda fijo tras crear la habitación. Este inventario no es disponibilidad por fechas.'
            : 'Room numbers are unique within the hotel. The type is fixed after creation. This inventory is not date-based availability.'}
        </p>
      </fieldset>
      <EditError error={error} es={es} onReload={onReload} />
      <button type="submit" className="button button-cyan" disabled={busy}>
        {busy ? (es ? 'Guardando…' : 'Saving…') : es ? 'Guardar habitación' : 'Save room'}
      </button>
    </form>
  )
}

async function allRoomTypes(signal) {
  const items = []
  for (let page = 1; page <= 100; page++) {
    const result = await catalogApi(`/admin/room-types?page=${page}&pageSize=100`, { signal })
    items.push(...result.items)
    if (page >= result.totalPages) return items
  }
  throw new Error('Catalogue too large for selector')
}

export default function AdminCatalog({ language, onAccessLost }) {
  const es = language === 'es'
  const [section, setSection] = useState('types')
  const [data, setData] = useState(null)
  const [status, setStatus] = useState('loading')
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [active, setActive] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [editor, setEditor] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [saved, setSaved] = useState(false)
  useEffect(() => {
    const controller = new AbortController()
    setStatus('loading')
    const timer = setTimeout(
      async () => {
        try {
          const query = new URLSearchParams({ page, pageSize: 20, search })
          if (active) query.set('active', active)
          const [hotel, assets, types, listing] = await Promise.all([
            catalogApi('/admin/hotel', { signal: controller.signal }),
            catalogApi('/admin/image-assets', { signal: controller.signal }),
            allRoomTypes(controller.signal),
            section === 'hotel'
              ? Promise.resolve(null)
              : catalogApi(`/admin/${section === 'types' ? 'room-types' : 'rooms'}?${query}`, {
                  signal: controller.signal,
                }),
          ])
          if (!controller.signal.aborted) {
            setData({ hotel, assets, types, listing })
            setStatus('ready')
          }
        } catch (error) {
          if (controller.signal.aborted) return
          if (error.status === 401 || error.status === 403) onAccessLost(error.status)
          else {
            setError(error)
            setStatus('error')
          }
        }
      },
      search ? 250 : 0,
    )
    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [section, page, search, active, attempt, onAccessLost])
  const reload = () => {
    setEditor(null)
    setError(null)
    setSaved(false)
    setAttempt(attempt + 1)
  }
  async function save(body, kind, id) {
    setBusy(true)
    setError(null)
    setSaved(false)
    try {
      await catalogApi(`/admin/${kind}${id ? `/${id}` : ''}`, {
        method: kind === 'hotel' || id ? 'PUT' : 'POST',
        body,
      })
      setEditor(null)
      setSaved(true)
      setAttempt((attempt) => attempt + 1)
    } catch (error) {
      if (error.status === 401 || error.status === 403) onAccessLost(error.status)
      else setError(error)
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="admin-catalog">
      <div
        className="catalog-tabs"
        aria-label={es ? 'Secciones administrativas' : 'Administration sections'}
      >
        {[
          ['types', es ? 'Tipos de habitación' : 'Room types'],
          ['rooms', es ? 'Habitaciones físicas' : 'Physical rooms'],
          ['hotel', 'Hotel'],
        ].map(([value, label]) => (
          <button
            key={value}
            aria-pressed={section === value}
            onClick={() => {
              setSection(value)
              setPage(1)
              setSearch('')
              setActive('')
              setError(null)
              setSaved(false)
            }}
          >
            {label}
          </button>
        ))}
      </div>
      <p className="catalog-demo-note">
        {es
          ? 'Catálogo académico · CNY · Tarifas, capacidades e inventario de demostración'
          : 'Academic catalogue · CNY · Demonstration rates, capacities and inventory'}
      </p>
      {saved && (
        <p className="catalog-success" role="status">
          {es ? 'Cambios guardados en el servidor.' : 'Changes saved on the server.'}
        </p>
      )}
      {section !== 'hotel' && (
        <div className="catalog-admin-tools">
          <label className="catalog-field">
            <span>{es ? 'Buscar registros' : 'Search records'}</span>
            <input
              type="search"
              value={search}
              maxLength={100}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
            />
          </label>
          <label className="catalog-field">
            <span>{es ? 'Estado' : 'Status'}</span>
            <select
              value={active}
              onChange={(event) => {
                setActive(event.target.value)
                setPage(1)
              }}
            >
              <option value="">{es ? 'Todos' : 'All'}</option>
              <option value="true">{es ? 'Activos' : 'Active'}</option>
              <option value="false">{es ? 'Inactivos' : 'Inactive'}</option>
            </select>
          </label>
          <button
            className="button button-cyan"
            disabled={status !== 'ready' || (section === 'rooms' && !data?.types.length)}
            onClick={() => {
              setError(null)
              setEditor({ kind: section, item: null })
            }}
          >
            {section === 'types'
              ? es
                ? 'Nuevo tipo'
                : 'New room type'
              : es
                ? 'Nueva habitación'
                : 'New room'}
          </button>
        </div>
      )}
      {status === 'loading' && (
        <p role="status">{es ? 'Cargando catálogo…' : 'Loading catalogue…'}</p>
      )}
      {!editor && error && <EditError error={error} es={es} onReload={reload} />}
      {status === 'error' && (
        <button className="button button-outline" onClick={reload}>
          {es ? 'Reintentar catálogo' : 'Retry catalogue'}
        </button>
      )}
      {status === 'ready' &&
        data &&
        (section === 'hotel' ? (
          <HotelEditor
            key={data.hotel.revision}
            hotel={data.hotel}
            es={es}
            busy={busy}
            error={error}
            onReload={reload}
            onSave={(body) => save(body, 'hotel')}
          />
        ) : (
          <>
            <p className="catalog-count" role="status">
              {data.listing.total} {es ? 'registros' : 'records'}
            </p>
            <ul className="catalog-admin-list">
              {data.listing.items.map((item) => (
                <li key={item.id}>
                  {section === 'types' && (
                    <img
                      src={item.images[0]?.url}
                      alt={es ? item.images[0]?.altEs : item.images[0]?.altEn}
                    />
                  )}
                  <div>
                    <h2>{section === 'types' ? (es ? item.nameEs : item.nameEn) : item.number}</h2>
                    <p>
                      {section === 'types'
                        ? `${money(item.nightlyRate, language)} / ${es ? 'noche' : 'night'} · ${item.capacity} ${es ? 'huéspedes' : 'guests'} · ${item.activeRooms}/${item.totalRooms} ${es ? 'operativas' : 'operational'}`
                        : es
                          ? item.typeNameEs
                          : item.typeNameEn}
                    </p>
                    <span className={item.active ? 'catalog-state active' : 'catalog-state'}>
                      {item.active ? (es ? 'Activo' : 'Active') : es ? 'Inactivo' : 'Inactive'}
                    </span>
                  </div>
                  <button
                    className="button button-outline"
                    aria-label={`${es ? 'Editar' : 'Edit'} ${section === 'types' ? (es ? item.nameEs : item.nameEn) : item.number}`}
                    onClick={() => {
                      setError(null)
                      setEditor({ kind: section, item })
                    }}
                  >
                    {es ? 'Editar' : 'Edit'}
                    <Icon name="diagonal" size={16} />
                  </button>
                </li>
              ))}
            </ul>
            {data.listing.total === 0 && (
              <p>
                {es ? 'No hay registros con estos filtros.' : 'No records match these filters.'}
              </p>
            )}
            {data.listing.totalPages > 1 && (
              <nav
                className="catalog-pagination"
                aria-label={es ? 'Páginas administrativas' : 'Administration pages'}
              >
                <button
                  className="button button-outline"
                  disabled={page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  {es ? 'Anterior' : 'Previous'}
                </button>
                <span>
                  {page} / {data.listing.totalPages}
                </span>
                <button
                  className="button button-outline"
                  disabled={page >= data.listing.totalPages}
                  onClick={() => setPage(page + 1)}
                >
                  {es ? 'Siguiente' : 'Next'}
                </button>
              </nav>
            )}
          </>
        ))}
      {editor && (
        <PreviewDialog
          closeDisabled={busy}
          title={
            editor.kind === 'types'
              ? es
                ? 'Tipo de habitación'
                : 'Room type'
              : es
                ? 'Habitación física'
                : 'Physical room'
          }
          closeLabel={es ? 'Cerrar' : 'Close'}
          onClose={() => {
            setEditor(null)
            setError(null)
          }}
        >
          {editor.kind === 'types' ? (
            <TypeEditor
              initial={editor.item}
              assets={data.assets}
              es={es}
              language={language}
              busy={busy}
              error={error}
              onReload={reload}
              onSave={(body) => save(body, 'room-types', editor.item?.id)}
            />
          ) : (
            <RoomEditor
              initial={editor.item}
              allTypes={data.types}
              es={es}
              busy={busy}
              error={error}
              onReload={reload}
              onSave={(body) => save(body, 'rooms', editor.item?.id)}
            />
          )}
        </PreviewDialog>
      )}
    </div>
  )
}
