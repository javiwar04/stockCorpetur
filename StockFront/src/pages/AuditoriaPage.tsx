import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import { listarAuditoria, type AuditoriaEvento } from '../features/auditoria/auditoriaApi';

const ACCIONES = [
  'Documento creado',
  'Documento actualizado',
  'Documento recibido',
  'Documento anulado',
  'Documento eliminado',
  'Pago proveedor registrado',
  'Pago proveedor eliminado',
  'Movimiento registrado',
  'Movimiento eliminado',
  'Conteo creado',
  'Ajustes de conteo aplicados',
  'Cierre mensual creado',
  'Cierre mensual anulado',
];

const ENTIDADES = [
  'DocumentoCompra',
  'PagoProveedor',
  'MovimientoInventario',
  'ConteoInventario',
  'CierreMensual',
];

function fechaInput(fecha: Date) {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function fechaHora(fecha: string) {
  return new Intl.DateTimeFormat('es-GT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(fecha));
}

function badgeAccion(accion: string) {
  if (accion.includes('anulado') || accion.includes('eliminado')) return 'badge-red';
  if (accion.includes('creado') || accion.includes('registrado')) return 'badge-green';
  if (accion.includes('actualizado') || accion.includes('aplicados')) return 'badge-amber';
  return 'badge-slate';
}

function EventoCard({ evento }: { evento: AuditoriaEvento }) {
  return (
    <div className="border-t border-slate-100 px-4 py-3 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className={badgeAccion(evento.accion)}>{evento.accion}</span>
            <span className="text-xs text-slate-400">{evento.entidad}{evento.entidadId ? ` #${evento.entidadId}` : ''}</span>
          </div>
          <div className="mt-2 font-semibold text-slate-800">{evento.resumen}</div>
          <div className="mt-1 text-xs text-slate-500">
            {evento.usuario} - {evento.hotel ?? 'Sin hotel'} - {fechaHora(evento.fecha)}
          </div>
        </div>
      </div>
      {evento.detalle && (
        <div className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">
          {evento.detalle}
        </div>
      )}
    </div>
  );
}

export function AuditoriaPage() {
  const hoy = new Date();
  const [hotelId, setHotelId] = useState<number | ''>('');
  const [accion, setAccion] = useState('');
  const [entidad, setEntidad] = useState('');
  const [desde, setDesde] = useState(() => fechaInput(new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - 30)));
  const [hasta, setHasta] = useState(() => fechaInput(hoy));

  const filtros = useMemo(
    () => ({
      hotelId: hotelId === '' ? undefined : hotelId,
      accion: accion || undefined,
      entidad: entidad || undefined,
      desde: desde || undefined,
      hasta: hasta || undefined,
    }),
    [hotelId, accion, entidad, desde, hasta],
  );

  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: eventos, isLoading } = useQuery({
    queryKey: ['auditoria', filtros],
    queryFn: () => listarAuditoria(filtros),
  });

  const limpiar = () => {
    setHotelId('');
    setAccion('');
    setEntidad('');
    setDesde('');
    setHasta('');
  };

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(56,189,248,0.24),rgba(15,23,42,0)_58%)]" />
          <div className="relative">
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-sky-300/80">Trazabilidad</p>
            <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Auditoria</h1>
            <p className="mt-1 max-w-2xl text-sm text-slate-300">
              Consulta eventos criticos: cierres, documentos, pagos, movimientos y conteos.
            </p>
          </div>
        </div>
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(180px,1fr)_minmax(220px,1fr)_minmax(180px,1fr)_150px_150px_auto] lg:items-end">
          <div>
            <label className="label">Hotel</label>
            <select value={hotelId} onChange={(e) => setHotelId(e.target.value === '' ? '' : Number(e.target.value))} className="field">
              <option value="">Todos</option>
              {hoteles?.map((h) => (
                <option key={h.id} value={h.id}>{h.nombre}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Accion</label>
            <select value={accion} onChange={(e) => setAccion(e.target.value)} className="field">
              <option value="">Todas</option>
              {ACCIONES.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Entidad</label>
            <select value={entidad} onChange={(e) => setEntidad(e.target.value)} className="field">
              <option value="">Todas</option>
              {ENTIDADES.map((e) => (
                <option key={e} value={e}>{e}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Desde</label>
            <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className="field" />
          </div>
          <div>
            <label className="label">Hasta</label>
            <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} className="field" />
          </div>
          <button type="button" onClick={limpiar} className="btn-secondary">
            Limpiar
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h2 className="card-title">Eventos recientes</h2>
            <p className="mt-1 text-xs text-slate-500">Se muestran hasta 300 eventos segun los filtros.</p>
          </div>
          {isLoading && <span className="badge-slate">Cargando</span>}
        </div>
        <div>
          {isLoading && <div className="empty-cell">Cargando auditoria...</div>}
          {!isLoading && eventos?.map((evento) => <EventoCard key={evento.id} evento={evento} />)}
          {!isLoading && eventos?.length === 0 && <div className="empty-cell">No hay eventos con estos filtros.</div>}
        </div>
      </div>
    </div>
  );
}
