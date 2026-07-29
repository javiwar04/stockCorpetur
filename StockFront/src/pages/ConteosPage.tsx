import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import { useAuth } from '../features/auth/authStore';
import {
  aplicarAjustesConteo,
  crearConteo,
  descargarConteosExcel,
  descargarConteosPdf,
  listarConteos,
  obtenerConteo,
  obtenerPlantillaConteo,
  type ConteoInventarioResumen,
  type PlantillaConteoItem,
} from '../features/conteos/conteosApi';

const numero = (n: number) => n.toLocaleString('es-GT', { maximumFractionDigits: 4 });
const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`;

function fechaInput(fecha: Date) {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function badgeEstado(estado: ConteoInventarioResumen['estado']) {
  if (estado === 'Ajustado') return 'badge-green';
  if (estado === 'Anulado') return 'badge-red';
  return 'badge-amber';
}

function Kpi({ titulo, valor, detalle, tono }: { titulo: string; valor: string; detalle: string; tono?: 'ok' | 'warn' | 'bad' }) {
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

export function ConteosPage() {
  const qc = useQueryClient();
  const { usuario, tieneRol } = useAuth();
  const esAdminOGerencia = tieneRol('Admin', 'Gerencia');
  const puedeEscribir = tieneRol('Admin', 'Gerencia', 'Digitador');

  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const [hotelId, setHotelId] = useState<number | ''>('');
  const [fecha, setFecha] = useState(() => fechaInput(new Date()));
  const [observaciones, setObservaciones] = useState('');
  const [cantidades, setCantidades] = useState<Record<number, string>>({});
  const [textoFiltro, setTextoFiltro] = useState('');
  const [soloDiferencias, setSoloDiferencias] = useState(false);
  const [conteoSeleccionadoId, setConteoSeleccionadoId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [descargando, setDescargando] = useState<'excel' | 'pdf' | null>(null);

  const hotelesVisibles = esAdminOGerencia
    ? hoteles
    : hoteles?.filter((h) => usuario?.hoteles.includes(h.id));

  useEffect(() => {
    if (hotelId === '' && usuario) {
      if (!esAdminOGerencia && usuario.hoteles.length > 0) setHotelId(usuario.hoteles[0]);
      else if (esAdminOGerencia && hoteles?.length) setHotelId(hoteles[0].id);
    }
  }, [usuario, hoteles, esAdminOGerencia, hotelId]);

  const hotelSeleccionado = hotelesVisibles?.find((h) => h.id === hotelId);

  const { data: plantilla, isLoading: cargandoPlantilla } = useQuery({
    queryKey: ['conteo-plantilla', hotelId, fecha],
    queryFn: () => obtenerPlantillaConteo(Number(hotelId), fecha),
    enabled: hotelId !== '',
  });

  const { data: conteos } = useQuery({
    queryKey: ['conteos-inventario', hotelId],
    queryFn: () => listarConteos({ hotelId: hotelId === '' ? undefined : Number(hotelId) }),
  });

  const { data: conteoSeleccionado } = useQuery({
    queryKey: ['conteo-inventario', conteoSeleccionadoId],
    queryFn: () => obtenerConteo(Number(conteoSeleccionadoId)),
    enabled: conteoSeleccionadoId != null,
  });

  const filasCaptura = useMemo(() => {
    const texto = textoFiltro.trim().toLowerCase();
    return (plantilla ?? []).filter((p) => {
      const valor = cantidades[p.productoId];
      const fisico = valor === undefined || valor === '' ? null : Number(valor);
      const diferencia = fisico == null ? 0 : fisico - p.existenciaSistemaBase;
      const coincideTexto = !texto || p.producto.toLowerCase().includes(texto) || p.categoria.toLowerCase().includes(texto);
      const coincideDiferencia = !soloDiferencias || Math.abs(diferencia) > 0;
      return coincideTexto && coincideDiferencia;
    });
  }, [plantilla, cantidades, textoFiltro, soloDiferencias]);

  const resumenCaptura = useMemo(() => {
    const capturadas = (plantilla ?? [])
      .map((p) => {
        const valor = cantidades[p.productoId];
        if (valor === undefined || valor === '') return null;
        const fisico = Number(valor);
        const diferencia = fisico - p.existenciaSistemaBase;
        return {
          diferencia,
          valor: diferencia * p.ultimoPrecioBase,
        };
      })
      .filter((x): x is { diferencia: number; valor: number } => x != null);

    return {
      productos: capturadas.length,
      diferencias: capturadas.filter((x) => Math.abs(x.diferencia) > 0).length,
      valor: capturadas.reduce((acc, x) => acc + Math.abs(x.valor), 0),
    };
  }, [plantilla, cantidades]);

  const crearMutation = useMutation({
    mutationFn: crearConteo,
    onSuccess: (conteo) => {
      qc.invalidateQueries({ queryKey: ['conteos-inventario'] });
      setConteoSeleccionadoId(conteo.id);
      setCantidades({});
      setObservaciones('');
      setError(null);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'No se pudo guardar el conteo.');
    },
  });

  const aplicarMutation = useMutation({
    mutationFn: aplicarAjustesConteo,
    onSuccess: (conteo) => {
      qc.invalidateQueries({ queryKey: ['conteos-inventario'] });
      qc.invalidateQueries({ queryKey: ['conteo-inventario', conteo.id] });
      qc.invalidateQueries({ queryKey: ['existencias'] });
      qc.invalidateQueries({ queryKey: ['movimientos'] });
      qc.invalidateQueries({ queryKey: ['kardex'] });
      setError(null);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'No se pudieron aplicar los ajustes.');
    },
  });

  const copiarSistema = () => {
    const valores: Record<number, string> = {};
    for (const p of plantilla ?? []) valores[p.productoId] = String(p.existenciaSistemaBase);
    setCantidades(valores);
  };

  const limpiarCaptura = () => setCantidades({});

  const guardarConteo = (e: React.FormEvent) => {
    e.preventDefault();
    if (hotelId === '') {
      setError('Selecciona un hotel.');
      return;
    }

    const detalles = Object.entries(cantidades)
      .filter(([, valor]) => valor !== '')
      .map(([productoId, valor]) => ({ productoId: Number(productoId), cantidadFisicaBase: Number(valor) }))
      .filter((d) => Number.isFinite(d.cantidadFisicaBase));

    if (detalles.length === 0) {
      setError('Captura al menos un producto.');
      return;
    }

    if (detalles.some((d) => d.cantidadFisicaBase < 0)) {
      setError('La cantidad fisica no puede ser negativa.');
      return;
    }

    crearMutation.mutate({
      hotelId: Number(hotelId),
      fecha,
      observaciones: observaciones || undefined,
      detalles,
    });
  };

  const aplicarAjustes = () => {
    if (!conteoSeleccionado || !window.confirm(`Aplicar ajustes del conteo #${conteoSeleccionado.id}?`)) return;
    aplicarMutation.mutate(conteoSeleccionado.id);
  };

  const exportar = async (formato: 'excel' | 'pdf') => {
    setDescargando(formato);
    setError(null);
    try {
      const filtros = { hotelId: hotelId === '' ? undefined : Number(hotelId) };
      await (formato === 'excel' ? descargarConteosExcel(filtros) : descargarConteosPdf(filtros));
    } catch {
      setError('No se pudo exportar el reporte de conteos.');
    } finally {
      setDescargando(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(245,158,11,0.24),rgba(15,23,42,0)_58%)]" />
          <div className="relative flex flex-wrap items-end justify-between gap-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-amber-300/80">Inventario real</p>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Conteos fisicos</h1>
              <p className="mt-1 text-sm text-slate-300">
                Captura existencias reales, compara contra sistema y aplica ajustes controlados.
              </p>
            </div>
            <select
              value={hotelId}
              onChange={(e) => setHotelId(e.target.value === '' ? '' : Number(e.target.value))}
              className="field w-full bg-white sm:w-auto sm:min-w-64"
            >
              {hotelesVisibles?.map((h) => (
                <option key={h.id} value={h.id}>
                  {h.nombre}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Kpi titulo="Productos capturados" valor={String(resumenCaptura.productos)} detalle="lineas listas para guardar" />
        <Kpi
          titulo="Con diferencia"
          valor={String(resumenCaptura.diferencias)}
          detalle="requieren ajuste si se aprueba"
          tono={resumenCaptura.diferencias > 0 ? 'warn' : 'ok'}
        />
        <Kpi
          titulo="Impacto estimado"
          valor={Q(resumenCaptura.valor)}
          detalle="valor absoluto de diferencias"
          tono={resumenCaptura.valor > 0 ? 'warn' : 'ok'}
        />
      </div>

      {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</div>}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1.4fr)_minmax(360px,0.8fr)]">
        {puedeEscribir && (
        <form onSubmit={guardarConteo} className="card overflow-hidden">
          <div className="card-header">
            <div>
              <h2 className="card-title">Nuevo conteo</h2>
              <p className="mt-1 text-xs text-slate-500">
                {hotelSeleccionado ? `${hotelSeleccionado.nombre}: existencia sistema al ${fecha}.` : 'Selecciona hotel.'}
              </p>
            </div>
            <div className="flex flex-wrap justify-end gap-2">
              <button type="button" onClick={copiarSistema} disabled={!plantilla?.length} className="btn-secondary btn-sm">
                Copiar sistema
              </button>
              <button type="button" onClick={limpiarCaptura} className="btn-secondary btn-sm">
                Limpiar
              </button>
              <button type="submit" disabled={crearMutation.isPending} className="btn-primary btn-sm">
                {crearMutation.isPending ? 'Guardando...' : 'Guardar conteo'}
              </button>
            </div>
          </div>

          <div className="border-b border-slate-200 bg-slate-50/70 px-4 py-4">
            <div className="grid grid-cols-1 gap-3 lg:grid-cols-[150px_minmax(200px,1fr)_auto] lg:items-end">
              <div>
                <label className="label">Fecha</label>
                <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} className="field bg-white" />
              </div>
              <div>
                <label className="label">Observaciones</label>
                <input
                  value={observaciones}
                  onChange={(e) => setObservaciones(e.target.value)}
                  placeholder="Conteo cierre, bodega, cocina..."
                  className="field bg-white"
                />
              </div>
              <label className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-600">
                <input type="checkbox" checked={soloDiferencias} onChange={(e) => setSoloDiferencias(e.target.checked)} />
                Solo diferencias
              </label>
            </div>
            <div className="mt-3">
              <label className="label">Buscar</label>
              <input
                value={textoFiltro}
                onChange={(e) => setTextoFiltro(e.target.value)}
                placeholder="Producto o categoria"
                className="field bg-white"
              />
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="thead">
                <tr>
                  <th className="th">Producto</th>
                  <th className="th text-right">Sistema</th>
                  <th className="th text-right">Fisico</th>
                  <th className="th text-right">Diferencia</th>
                  <th className="th text-right">Valor</th>
                </tr>
              </thead>
              <tbody>
                {cargandoPlantilla && (
                  <tr>
                    <td colSpan={5} className="empty-cell">Cargando plantilla...</td>
                  </tr>
                )}
                {!cargandoPlantilla && filasCaptura.map((p: PlantillaConteoItem) => {
                  const valor = cantidades[p.productoId] ?? '';
                  const fisico = valor === '' ? null : Number(valor);
                  const diferencia = fisico == null ? 0 : fisico - p.existenciaSistemaBase;
                  const valorDiferencia = diferencia * p.ultimoPrecioBase;
                  return (
                    <tr key={p.productoId} className="trow">
                      <td className="td">
                        <div className="font-medium text-slate-800">{p.producto}</div>
                        <div className="text-xs text-slate-400">
                          {p.categoria} - {p.unidadBase}
                          {p.stockMinimoBase > 0 && ` - minimo ${numero(p.stockMinimoBase)}`}
                        </div>
                      </td>
                      <td className="td text-right text-slate-600">
                        {numero(p.existenciaSistemaBase)} {p.unidadBase}
                      </td>
                      <td className="td text-right">
                        <input
                          type="number"
                          min="0"
                          step="0.0001"
                          value={valor}
                          onChange={(e) => setCantidades((prev) => ({ ...prev, [p.productoId]: e.target.value }))}
                          className="field ml-auto w-28 py-1.5 text-right"
                          placeholder="-"
                        />
                      </td>
                      <td className="td text-right">
                        {valor === '' ? (
                          <span className="text-slate-400">-</span>
                        ) : (
                          <span className={diferencia < 0 ? 'text-rose-700' : diferencia > 0 ? 'text-emerald-700' : 'text-slate-500'}>
                            {numero(diferencia)} {p.unidadBase}
                          </span>
                        )}
                      </td>
                      <td className="td text-right font-semibold text-slate-800">
                        {valor === '' ? '-' : Q(Math.abs(valorDiferencia))}
                      </td>
                    </tr>
                  );
                })}
                {!cargandoPlantilla && filasCaptura.length === 0 && (
                  <tr>
                    <td colSpan={5} className="empty-cell">Sin productos para los filtros.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </form>
        )}

        <div className="space-y-6">
          <div className="card overflow-hidden">
            <div className="card-header">
              <div>
                <h2 className="card-title">Historial</h2>
                <p className="mt-1 text-xs text-slate-500">Ultimos conteos del hotel seleccionado.</p>
              </div>
              <div className="flex flex-wrap justify-end gap-2">
                <button
                  type="button"
                  onClick={() => void exportar('excel')}
                  disabled={descargando !== null}
                  className="btn-secondary btn-sm"
                >
                  {descargando === 'excel' ? 'Exportando...' : 'Excel'}
                </button>
                <button
                  type="button"
                  onClick={() => void exportar('pdf')}
                  disabled={descargando !== null}
                  className="btn-secondary btn-sm"
                >
                  {descargando === 'pdf' ? 'Exportando...' : 'PDF'}
                </button>
              </div>
            </div>
            <div className="divide-y divide-slate-100">
              {conteos?.map((c) => (
                <button
                  type="button"
                  key={c.id}
                  onClick={() => setConteoSeleccionadoId(c.id)}
                  className={`block w-full px-4 py-3 text-left text-sm transition-colors hover:bg-slate-50 ${
                    conteoSeleccionadoId === c.id ? 'bg-emerald-50/70' : ''
                  }`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-semibold text-slate-800">#{c.id} - {c.fecha}</div>
                      <div className="mt-0.5 text-xs text-slate-500">
                        {c.productosContados} productos - {c.productosConDiferencia} con diferencia
                      </div>
                    </div>
                    <span className={badgeEstado(c.estado)}>{c.estado}</span>
                  </div>
                  <div className="mt-2 flex items-center justify-between text-xs text-slate-500">
                    <span>{c.creadoPor ?? 'sistema'}</span>
                    <span className="font-semibold text-slate-700">{Q(c.valorDiferenciaEstimado)}</span>
                  </div>
                </button>
              ))}
              {conteos?.length === 0 && <div className="empty-cell">Aun no hay conteos para este hotel.</div>}
            </div>
          </div>

          <div className="card overflow-hidden">
            <div className="card-header">
              <div>
                <h2 className="card-title">Detalle del conteo</h2>
                <p className="mt-1 text-xs text-slate-500">Diferencias congeladas y ajustes aplicados.</p>
              </div>
              {conteoSeleccionado && <span className={badgeEstado(conteoSeleccionado.estado)}>{conteoSeleccionado.estado}</span>}
            </div>
            {conteoSeleccionado ? (
              <>
                <div className="border-b border-slate-200 px-4 py-3 text-sm">
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <div className="text-xs text-slate-400">Fecha</div>
                      <div className="font-semibold text-slate-800">{conteoSeleccionado.fecha}</div>
                    </div>
                    <div>
                      <div className="text-xs text-slate-400">Impacto</div>
                      <div className="font-semibold text-slate-800">{Q(conteoSeleccionado.valorDiferenciaEstimado)}</div>
                    </div>
                  </div>
                  {conteoSeleccionado.observaciones && (
                    <div className="mt-3 rounded-lg bg-slate-50 px-3 py-2 text-xs text-slate-600">{conteoSeleccionado.observaciones}</div>
                  )}
                  {esAdminOGerencia && conteoSeleccionado.estado === 'Registrado' && (
                    <button
                      type="button"
                      onClick={aplicarAjustes}
                      disabled={aplicarMutation.isPending || conteoSeleccionado.productosConDiferencia === 0}
                      className="btn-primary btn-sm mt-3"
                    >
                      {aplicarMutation.isPending ? 'Aplicando...' : 'Aplicar ajustes'}
                    </button>
                  )}
                  {conteoSeleccionado.estado === 'Ajustado' && (
                    <div className="mt-3 text-xs text-slate-500">
                      Ajustado por {conteoSeleccionado.ajustesAplicadosPor ?? 'sistema'}.
                    </div>
                  )}
                </div>
                <div className="max-h-[520px] overflow-y-auto">
                  <table className="w-full text-sm">
                    <thead className="thead sticky top-0">
                      <tr>
                        <th className="th">Producto</th>
                        <th className="th text-right">Sistema</th>
                        <th className="th text-right">Fisico</th>
                        <th className="th text-right">Dif.</th>
                      </tr>
                    </thead>
                    <tbody>
                      {conteoSeleccionado.detalles.map((d) => (
                        <tr key={d.id} className="trow">
                          <td className="td">
                            <div className="font-medium text-slate-800">{d.producto}</div>
                            <div className="text-xs text-slate-400">{Q(Math.abs(d.valorDiferenciaEstimado))}</div>
                          </td>
                          <td className="td text-right text-slate-600">{numero(d.cantidadSistemaBase)}</td>
                          <td className="td text-right text-slate-600">{numero(d.cantidadFisicaBase)}</td>
                          <td className={`td text-right font-semibold ${d.diferenciaBase < 0 ? 'text-rose-700' : d.diferenciaBase > 0 ? 'text-emerald-700' : 'text-slate-500'}`}>
                            {numero(d.diferenciaBase)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            ) : (
              <div className="empty-cell">Selecciona un conteo para ver el detalle.</div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
