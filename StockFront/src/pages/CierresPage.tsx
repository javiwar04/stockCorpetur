import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import {
  anularCierreMensual,
  cerrarMes,
  descargarCierresExcel,
  descargarCierresPdf,
  listarCierresMensuales,
  previewCierreMensual,
  type CierreMensual,
} from '../features/cierres/cierresApi';

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const MESES = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
];

function periodoInicial() {
  const hoy = new Date();
  const anterior = new Date(hoy.getFullYear(), hoy.getMonth() - 1, 1);
  return { anio: anterior.getFullYear(), mes: anterior.getMonth() + 1 };
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

function badgeEstado(estado: CierreMensual['estado']) {
  if (estado === 'Cerrado') return 'badge-green';
  if (estado === 'Anulado') return 'badge-red';
  return 'badge-amber';
}

function Kpi({
  titulo,
  valor,
  detalle,
  tono,
}: {
  titulo: string;
  valor: string;
  detalle: string;
  tono?: 'ok' | 'warn' | 'bad';
}) {
  const color =
    tono === 'bad'
      ? 'text-rose-600'
      : tono === 'warn'
        ? 'text-amber-600'
        : tono === 'ok'
          ? 'text-emerald-600'
          : 'text-slate-900';

  return (
    <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-400">{titulo}</div>
      <div className={`mt-2 text-2xl font-semibold tracking-tight ${color}`}>{valor}</div>
      <div className="mt-1 text-xs text-slate-500">{detalle}</div>
    </div>
  );
}

function ResumenOperativo({ cierre }: { cierre?: CierreMensual }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Kpi
        titulo="Compras del mes"
        valor={Q(cierre?.comprasTotal ?? 0)}
        detalle={`${cierre?.documentosCompra ?? 0} documentos recibidos`}
      />
      <Kpi
        titulo="Inventario valorizado"
        valor={Q(cierre?.valorInventarioEstimado ?? 0)}
        detalle="existencia al cierre"
      />
      <Kpi
        titulo="Stock en riesgo"
        valor={String(cierre?.productosEnRiesgo ?? 0)}
        detalle={`${Q(cierre?.valorFaltanteEstimado ?? 0)} faltante estimado`}
        tono={(cierre?.productosEnRiesgo ?? 0) > 0 ? 'warn' : 'ok'}
      />
      <Kpi
        titulo="Cuentas por pagar"
        valor={Q(cierre?.saldoCuentasPorPagar ?? 0)}
        detalle={`${Q(cierre?.saldoCuentasVencido ?? 0)} vencido`}
        tono={(cierre?.saldoCuentasVencido ?? 0) > 0 ? 'bad' : undefined}
      />
    </div>
  );
}

function ResumenControl({ cierre }: { cierre?: CierreMensual }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Kpi
        titulo="Mermas"
        valor={Q(cierre?.valorMermasEstimado ?? 0)}
        detalle={`${cierre?.movimientosMerma ?? 0} movimientos`}
        tono={(cierre?.valorMermasEstimado ?? 0) > 0 ? 'warn' : undefined}
      />
      <Kpi
        titulo="Ajustes"
        valor={Q(cierre?.valorAjustesEstimado ?? 0)}
        detalle={`${cierre?.movimientosAjuste ?? 0} movimientos`}
      />
      <Kpi
        titulo="Conteos fisicos"
        valor={String(cierre?.conteosFisicos ?? 0)}
        detalle={`${Q(cierre?.valorDiferenciasConteo ?? 0)} en diferencias`}
      />
      <Kpi
        titulo="Facturas vencidas"
        valor={String(cierre?.documentosVencidos ?? 0)}
        detalle="pendientes al cierre"
        tono={(cierre?.documentosVencidos ?? 0) > 0 ? 'bad' : 'ok'}
      />
    </div>
  );
}

export function CierresPage() {
  const qc = useQueryClient();
  const inicial = useMemo(periodoInicial, []);
  const [hotelId, setHotelId] = useState<number | ''>('');
  const [anio, setAnio] = useState(inicial.anio);
  const [mes, setMes] = useState(inicial.mes);
  const [observaciones, setObservaciones] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [descargando, setDescargando] = useState<'excel' | 'pdf' | null>(null);

  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });

  useEffect(() => {
    if (hotelId === '' && hoteles?.length) setHotelId(hoteles[0].id);
  }, [hoteles, hotelId]);

  const filtrosHistorial = useMemo(
    () => ({
      hotelId: hotelId === '' ? undefined : hotelId,
      anio,
    }),
    [hotelId, anio],
  );

  const { data: cierres, isLoading: cargandoHistorial } = useQuery({
    queryKey: ['cierres-mensuales', filtrosHistorial],
    queryFn: () => listarCierresMensuales(filtrosHistorial),
  });

  const { data: preview, isLoading: cargandoPreview } = useQuery({
    queryKey: ['cierre-preview', hotelId, anio, mes],
    queryFn: () => previewCierreMensual(Number(hotelId), anio, mes),
    enabled: hotelId !== '',
  });

  const cierreExistente = cierres?.find((c) => c.hotelId === hotelId && c.anio === anio && c.mes === mes && c.estado === 'Cerrado');
  const cierreMostrado = cierreExistente ?? preview;
  const hotelSeleccionado = hoteles?.find((h) => h.id === hotelId);

  const cerrarMutation = useMutation({
    mutationFn: cerrarMes,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cierres-mensuales'] });
      qc.invalidateQueries({ queryKey: ['cierre-preview'] });
      qc.invalidateQueries({ queryKey: ['dashboard-gerencial'] });
      setObservaciones('');
      setError(null);
    },
    onError: (e: unknown) => {
      const msg =
        (e as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'No se pudo cerrar el mes.';
      setError(msg);
    },
  });

  const anularMutation = useMutation({
    mutationFn: ({ id, motivo }: { id: number; motivo?: string }) => anularCierreMensual(id, { motivo }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cierres-mensuales'] });
      qc.invalidateQueries({ queryKey: ['cierre-preview'] });
      qc.invalidateQueries({ queryKey: ['dashboard-gerencial'] });
      setError(null);
    },
    onError: (e: unknown) => {
      const msg =
        (e as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'No se pudo anular el cierre.';
      setError(msg);
    },
  });

  const cerrarPeriodo = () => {
    if (hotelId === '') {
      setError('Selecciona un hotel.');
      return;
    }
    const nombrePeriodo = `${MESES[mes - 1]} ${anio}`;
    if (!window.confirm(`Cerrar ${nombrePeriodo} para ${hotelSeleccionado?.nombre ?? 'este hotel'}?`)) return;

    cerrarMutation.mutate({
      hotelId: Number(hotelId),
      anio,
      mes,
      observaciones: observaciones || undefined,
    });
  };

  const anularPeriodo = () => {
    if (!cierreExistente) return;
    const motivo = window.prompt('Motivo de anulacion del cierre mensual:');
    if (motivo == null) return;
    if (!window.confirm('Anular este cierre y reabrir el periodo para correcciones?')) return;
    anularMutation.mutate({ id: cierreExistente.id, motivo: motivo || undefined });
  };

  const exportar = async (formato: 'excel' | 'pdf') => {
    if (hotelId === '' || !cierreExistente) return;
    setDescargando(formato);
    setError(null);
    try {
      const filtros = { hotelId: Number(hotelId), anio, mes };
      await (formato === 'excel' ? descargarCierresExcel(filtros) : descargarCierresPdf(filtros));
    } catch {
      setError('No se pudo exportar el cierre mensual.');
    } finally {
      setDescargando(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(16,185,129,0.26),rgba(15,23,42,0)_58%)]" />
          <div className="relative flex flex-wrap items-end justify-between gap-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-emerald-300/80">Control gerencial</p>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Cierre mensual</h1>
              <p className="mt-1 max-w-2xl text-sm text-slate-300">
                Congela compras, inventario, conteos, mermas, ajustes y cuentas por pagar por hotel.
              </p>
            </div>
            {cierreMostrado && <span className={badgeEstado(cierreMostrado.estado)}>{cierreMostrado.estado}</span>}
          </div>
        </div>
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_150px_minmax(220px,1.2fr)_auto] lg:items-end">
          <div>
            <label className="label">Hotel</label>
            <select value={hotelId} onChange={(e) => setHotelId(e.target.value === '' ? '' : Number(e.target.value))} className="field">
              {hoteles?.map((h) => (
                <option key={h.id} value={h.id}>{h.nombre}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Mes</label>
            <select value={mes} onChange={(e) => setMes(Number(e.target.value))} className="field">
              {MESES.map((nombre, index) => (
                <option key={nombre} value={index + 1}>{nombre}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Anio</label>
            <input
              type="number"
              min="2020"
              max="2100"
              value={anio}
              onChange={(e) => setAnio(Number(e.target.value))}
              className="field"
            />
          </div>
          <div>
            <label className="label">Observaciones del cierre</label>
            <input
              value={observaciones}
              onChange={(e) => setObservaciones(e.target.value)}
              disabled={!!cierreExistente}
              placeholder="Notas internas del periodo"
              className="field"
            />
          </div>
          <button
            type="button"
            onClick={cerrarPeriodo}
            disabled={hotelId === '' || !!cierreExistente || cerrarMutation.isPending || anularMutation.isPending}
            className="btn-primary"
          >
            {cerrarMutation.isPending ? 'Cerrando...' : cierreExistente ? 'Periodo cerrado' : 'Cerrar mes'}
          </button>
        </div>
      </div>

      {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</div>}
      {cargandoPreview && <div className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-500">Calculando preview...</div>}

      <ResumenOperativo cierre={cierreMostrado} />
      <ResumenControl cierre={cierreMostrado} />

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(360px,0.65fr)]">
        <div className="card overflow-hidden">
          <div className="card-header">
            <div>
              <h2 className="card-title">Snapshot del periodo</h2>
              <p className="mt-1 text-xs text-slate-500">
                {hotelSeleccionado?.nombre ?? 'Hotel'} - {MESES[mes - 1]} {anio}
              </p>
            </div>
            <div className="flex flex-wrap justify-end gap-2">
              {cierreExistente ? (
                <span className="badge-green">Guardado</span>
              ) : (
                <span className="badge-amber">Preview</span>
              )}
              <button
                type="button"
                onClick={() => void exportar('excel')}
                disabled={!cierreExistente || descargando !== null}
                className="btn-secondary btn-sm"
              >
                {descargando === 'excel' ? 'Exportando...' : 'Excel'}
              </button>
              <button
                type="button"
                onClick={() => void exportar('pdf')}
                disabled={!cierreExistente || descargando !== null}
                className="btn-secondary btn-sm"
              >
                {descargando === 'pdf' ? 'Exportando...' : 'PDF'}
              </button>
              {cierreExistente && (
                <button
                  type="button"
                  onClick={anularPeriodo}
                  disabled={anularMutation.isPending || descargando !== null}
                  className="btn-danger btn-sm"
                >
                  {anularMutation.isPending ? 'Anulando...' : 'Anular cierre'}
                </button>
              )}
            </div>
          </div>
          <div className="grid grid-cols-1 divide-y divide-slate-100 md:grid-cols-2 md:divide-x md:divide-y-0">
            <div className="space-y-3 p-5">
              <h3 className="text-sm font-semibold text-slate-800">Operacion</h3>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between gap-3"><span className="text-slate-500">Compras recibidas</span><span className="font-semibold">{Q(cierreMostrado?.comprasTotal ?? 0)}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">Documentos</span><span className="font-semibold">{cierreMostrado?.documentosCompra ?? 0}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">Inventario estimado</span><span className="font-semibold">{Q(cierreMostrado?.valorInventarioEstimado ?? 0)}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">Faltante estimado</span><span className="font-semibold">{Q(cierreMostrado?.valorFaltanteEstimado ?? 0)}</span></div>
              </div>
            </div>
            <div className="space-y-3 p-5">
              <h3 className="text-sm font-semibold text-slate-800">Control</h3>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between gap-3"><span className="text-slate-500">Mermas</span><span className="font-semibold">{Q(cierreMostrado?.valorMermasEstimado ?? 0)}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">Ajustes</span><span className="font-semibold">{Q(cierreMostrado?.valorAjustesEstimado ?? 0)}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">Diferencias de conteo</span><span className="font-semibold">{Q(cierreMostrado?.valorDiferenciasConteo ?? 0)}</span></div>
                <div className="flex justify-between gap-3"><span className="text-slate-500">CXP vencido</span><span className="font-semibold">{Q(cierreMostrado?.saldoCuentasVencido ?? 0)}</span></div>
              </div>
            </div>
          </div>
          {cierreExistente?.observaciones && (
            <div className="border-t border-slate-100 bg-slate-50 px-5 py-3 text-sm text-slate-600">{cierreExistente.observaciones}</div>
          )}
        </div>

        <div className="card overflow-hidden">
          <div className="card-header">
            <div>
              <h2 className="card-title">Historial</h2>
              <p className="mt-1 text-xs text-slate-500">Cierres guardados del anio seleccionado.</p>
            </div>
            {cargandoHistorial && <span className="badge-slate">Cargando</span>}
          </div>
          <div className="divide-y divide-slate-100">
            {cierres?.map((c) => (
              <div key={c.id} className="px-4 py-3 text-sm">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold text-slate-800">{MESES[c.mes - 1]} {c.anio}</div>
                    <div className="mt-0.5 text-xs text-slate-500">{c.hotel}</div>
                  </div>
                  <span className={badgeEstado(c.estado)}>{c.estado}</span>
                </div>
                <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                  <div className="rounded-lg bg-slate-50 px-3 py-2">
                    <div className="text-slate-400">Compras</div>
                    <div className="font-semibold text-slate-800">{Q(c.comprasTotal)}</div>
                  </div>
                  <div className="rounded-lg bg-slate-50 px-3 py-2">
                    <div className="text-slate-400">Inventario</div>
                    <div className="font-semibold text-slate-800">{Q(c.valorInventarioEstimado)}</div>
                  </div>
                </div>
                <div className="mt-2 text-xs text-slate-400">
                  Cerrado por {c.creadoPor ?? 'sistema'} - {fechaHora(c.fechaCierre)}
                </div>
              </div>
            ))}
            {cierres?.length === 0 && <div className="empty-cell">No hay cierres guardados con estos filtros.</div>}
          </div>
        </div>
      </div>
    </div>
  );
}
