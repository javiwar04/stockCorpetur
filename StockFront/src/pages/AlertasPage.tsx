import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listarAlertas, type Alerta, type SeveridadAlerta } from '../features/alertas/alertasApi';

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`;

const SEVERIDADES: Array<SeveridadAlerta | ''> = ['', 'Critica', 'Alta', 'Media', 'Baja'];

function badgeSeveridad(severidad: SeveridadAlerta) {
  if (severidad === 'Critica') return 'badge-red';
  if (severidad === 'Alta') return 'badge-amber';
  if (severidad === 'Media') return 'badge-sky';
  return 'badge-slate';
}

function fecha(fechaIso: string | null) {
  if (!fechaIso) return '-';
  return new Intl.DateTimeFormat('es-GT', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${fechaIso}T00:00:00`));
}

function Kpi({ titulo, valor, detalle, tono }: { titulo: string; valor: string; detalle: string; tono?: 'bad' | 'warn' | 'info' }) {
  const color = tono === 'bad' ? 'text-rose-600' : tono === 'warn' ? 'text-amber-600' : tono === 'info' ? 'text-sky-600' : 'text-slate-900';
  return (
    <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-400">{titulo}</div>
      <div className={`mt-2 text-2xl font-semibold tracking-tight ${color}`}>{valor}</div>
      <div className="mt-1 text-xs text-slate-500">{detalle}</div>
    </div>
  );
}

function AlertaCard({ alerta }: { alerta: Alerta }) {
  return (
    <div className="border-t border-slate-100 px-4 py-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className={badgeSeveridad(alerta.severidad)}>{alerta.severidad}</span>
            <span className="badge-slate">{alerta.tipo}</span>
            {alerta.hotel && <span className="text-xs text-slate-400">{alerta.hotel}</span>}
          </div>
          <div className="mt-2 text-sm font-semibold text-slate-900">{alerta.titulo}</div>
          <div className="mt-1 text-sm text-slate-600">{alerta.mensaje}</div>
          {alerta.accionSugerida && (
            <div className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">
              {alerta.accionSugerida}
            </div>
          )}
        </div>
        <div className="text-right text-xs text-slate-500">
          <div>{fecha(alerta.fecha)}</div>
          {alerta.monto != null && <div className="mt-1 font-semibold text-slate-800">{Q(alerta.monto)}</div>}
          {alerta.entidad && (
            <div className="mt-1">
              {alerta.entidad}{alerta.entidadId ? ` #${alerta.entidadId}` : ''}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export function AlertasPage() {
  const [severidad, setSeveridad] = useState<SeveridadAlerta | ''>('');
  const [tipo, setTipo] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['alertas'],
    queryFn: listarAlertas,
    refetchInterval: 60000,
  });

  const tipos = useMemo(
    () => Array.from(new Set((data?.alertas ?? []).map((a) => a.tipo))).sort(),
    [data?.alertas],
  );

  const alertas = useMemo(
    () => (data?.alertas ?? []).filter((a) => (!severidad || a.severidad === severidad) && (!tipo || a.tipo === tipo)),
    [data?.alertas, severidad, tipo],
  );

  const resumen = data?.resumen;

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(248,113,113,0.24),rgba(15,23,42,0)_58%)]" />
          <div className="relative">
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-rose-300/80">Monitoreo</p>
            <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Alertas internas</h1>
            <p className="mt-1 max-w-2xl text-sm text-slate-300">
              Stock critico, cuentas vencidas, conteos con diferencias fuertes y cierres pendientes.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <Kpi titulo="Total" valor={String(resumen?.total ?? 0)} detalle="alertas activas" />
        <Kpi titulo="Criticas" valor={String(resumen?.criticas ?? 0)} detalle="atender primero" tono="bad" />
        <Kpi titulo="Altas" valor={String(resumen?.altas ?? 0)} detalle="requieren seguimiento" tono="warn" />
        <Kpi titulo="Medias" valor={String(resumen?.medias ?? 0)} detalle="monitoreo operativo" tono="info" />
        <Kpi titulo="Bajas" valor={String(resumen?.bajas ?? 0)} detalle="informativas" />
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-[220px_260px_auto] sm:items-end">
          <div>
            <label className="label">Severidad</label>
            <select value={severidad} onChange={(e) => setSeveridad(e.target.value as SeveridadAlerta | '')} className="field">
              {SEVERIDADES.map((s) => (
                <option key={s || 'todas'} value={s}>{s || 'Todas'}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Tipo</label>
            <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="field">
              <option value="">Todos</option>
              {tipos.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </div>
          <button
            type="button"
            onClick={() => {
              setSeveridad('');
              setTipo('');
            }}
            className="btn-secondary"
          >
            Limpiar
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h2 className="card-title">Alertas activas</h2>
            <p className="mt-1 text-xs text-slate-500">Se recalculan en vivo con la informacion actual.</p>
          </div>
          {isLoading && <span className="badge-slate">Cargando</span>}
        </div>
        <div>
          {isLoading && <div className="empty-cell">Calculando alertas...</div>}
          {!isLoading && alertas.map((alerta) => <AlertaCard key={alerta.id} alerta={alerta} />)}
          {!isLoading && alertas.length === 0 && <div className="empty-cell">No hay alertas con estos filtros.</div>}
        </div>
      </div>
    </div>
  );
}
