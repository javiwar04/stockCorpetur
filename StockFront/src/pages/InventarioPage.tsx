import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  descargarKardexExcel,
  eliminarMovimiento,
  eliminarStockMinimo,
  guardarStockMinimo,
  listarMovimientos,
  obtenerKardex,
  obtenerExistencias,
  registrarMovimiento,
  sugerenciasCompra,
  type Existencia,
} from '../features/inventario/inventarioApi';
import {
  listarConversiones,
  listarHoteles,
  listarProductos,
} from '../features/catalogos/catalogosApi';
import type { Conversion } from '../features/catalogos/types';
import { useAuth } from '../features/auth/authStore';

const TIPOS = [
  { valor: 'Salida', etiqueta: 'Salida (consumo cocina)' },
  { valor: 'Merma', etiqueta: 'Merma (producto dañado)' },
  { valor: 'Ajuste', etiqueta: 'Ajuste (conteo físico, ±)' },
  { valor: 'Entrada', etiqueta: 'Entrada (sin compra)' },
] as const;

const BADGE_TIPO: Record<string, string> = {
  Compra: 'badge-green',
  Salida: 'badge-sky',
  Merma: 'badge-red',
  Ajuste: 'badge-amber',
  Entrada: 'badge-green',
};

const numero = (n: number) => n.toLocaleString('es-GT', { maximumFractionDigits: 2 });
const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

function fechaInput(fecha: Date) {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function KpiInventario({ titulo, valor, detalle, tono }: { titulo: string; valor: string; detalle: string; tono?: 'ok' | 'warn' | 'bad' }) {
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

function badgeEstado(e: Existencia) {
  if (e.estadoStock === 'Negativo') return 'badge-red';
  if (e.estadoStock === 'SinStock' || e.estadoStock === 'BajoMinimo') return 'badge-amber';
  if (e.estadoStock === 'Ok') return 'badge-green';
  return 'badge-slate';
}

function textoEstado(estado: Existencia['estadoStock']) {
  const labels: Record<Existencia['estadoStock'], string> = {
    Ok: 'OK',
    BajoMinimo: 'Bajo mínimo',
    SinStock: 'Sin stock',
    Negativo: 'Negativo',
    SinConfigurar: 'Sin mínimo',
  };
  return labels[estado];
}

export function InventarioPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { usuario, tieneRol } = useAuth();
  const esAdminOGerencia = tieneRol('Admin', 'Gerencia');

  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: productos } = useQuery({ queryKey: ['productos'], queryFn: () => listarProductos(true) });

  const [hotelId, setHotelId] = useState<number | ''>('');
  useEffect(() => {
    if (hotelId === '' && usuario) {
      if (!esAdminOGerencia && usuario.hoteles.length > 0) setHotelId(usuario.hoteles[0]);
      else if (esAdminOGerencia && hoteles?.length) setHotelId(hoteles[0].id);
    }
  }, [usuario, hoteles, esAdminOGerencia, hotelId]);

  const hotelesVisibles = esAdminOGerencia
    ? hoteles
    : hoteles?.filter((h) => usuario?.hoteles.includes(h.id));
  const hotelSeleccionado = hotelesVisibles?.find((h) => h.id === hotelId);

  const { data: existencias, isLoading } = useQuery({
    queryKey: ['existencias', hotelId],
    queryFn: () => obtenerExistencias(Number(hotelId)),
    enabled: hotelId !== '',
  });
  const { data: movimientos } = useQuery({
    queryKey: ['movimientos', hotelId],
    queryFn: () => listarMovimientos({ hotelId: Number(hotelId) }),
    enabled: hotelId !== '',
  });
  const [kardexProductoId, setKardexProductoId] = useState<number | ''>('');
  const [kardexDesde, setKardexDesde] = useState('');
  const [kardexHasta, setKardexHasta] = useState('');
  const [kardexDescargando, setKardexDescargando] = useState(false);
  const [kardexError, setKardexError] = useState<string | null>(null);
  const { data: kardex, isFetching: kardexCargando } = useQuery({
    queryKey: ['kardex', hotelId, kardexProductoId, kardexDesde, kardexHasta],
    queryFn: () =>
      obtenerKardex({
        hotelId: Number(hotelId),
        productoId: Number(kardexProductoId),
        desde: kardexDesde || undefined,
        hasta: kardexHasta || undefined,
      }),
    enabled: hotelId !== '' && kardexProductoId !== '',
  });
  const { data: sugerencias } = useQuery({
    queryKey: ['sugerencias-compra', hotelId],
    queryFn: () => sugerenciasCompra(Number(hotelId)),
    enabled: hotelId !== '',
  });

  const [filtroTexto, setFiltroTexto] = useState('');
  const [filtroCategoria, setFiltroCategoria] = useState('');
  const [soloRiesgo, setSoloRiesgo] = useState(false);
  const [minimosEdit, setMinimosEdit] = useState<Record<number, string>>({});

  useEffect(() => {
    const valores: Record<number, string> = {};
    for (const e of existencias ?? []) valores[e.productoId] = e.stockMinimo > 0 ? String(e.stockMinimo) : '';
    setMinimosEdit(valores);
  }, [existencias]);

  const categorias = useMemo(
    () => [...new Set((existencias ?? []).map((e) => e.categoria))].sort(),
    [existencias],
  );

  const existenciasFiltradas = useMemo(() => {
    const texto = filtroTexto.trim().toLowerCase();
    return (existencias ?? []).filter((e) => {
      const coincideTexto = !texto || e.producto.toLowerCase().includes(texto) || e.categoria.toLowerCase().includes(texto);
      const coincideCategoria = !filtroCategoria || e.categoria === filtroCategoria;
      const coincideRiesgo = !soloRiesgo || ['Negativo', 'SinStock', 'BajoMinimo'].includes(e.estadoStock);
      return coincideTexto && coincideCategoria && coincideRiesgo;
    });
  }, [existencias, filtroTexto, filtroCategoria, soloRiesgo]);

  const resumen = useMemo(() => {
    const filas = existencias ?? [];
    return {
      productos: filas.length,
      negativos: filas.filter((e) => e.estadoStock === 'Negativo').length,
      sinStock: filas.filter((e) => e.estadoStock === 'SinStock').length,
      bajoMinimo: filas.filter((e) => e.estadoStock === 'BajoMinimo').length,
      configurados: filas.filter((e) => e.stockMinimo > 0).length,
    };
  }, [existencias]);

  const totalSugerido = useMemo(
    () => (sugerencias ?? []).reduce((acc, s) => acc + (s.costoEstimado ?? 0), 0),
    [sugerencias],
  );

  const [tipo, setTipo] = useState<string>('Salida');
  const [fecha, setFecha] = useState(() => fechaInput(new Date()));
  const [productoId, setProductoId] = useState<number | ''>('');
  const [unidadId, setUnidadId] = useState<number | ''>('');
  const [cantidad, setCantidad] = useState('');
  const [referencia, setReferencia] = useState('');
  const [conversiones, setConversiones] = useState<Conversion[]>([]);
  const [error, setError] = useState<string | null>(null);

  const seleccionarProducto = async (id: number | '') => {
    setProductoId(id);
    setUnidadId('');
    if (id === '') {
      setConversiones([]);
      return;
    }
    setConversiones(await listarConversiones(id));
  };

  const registrarMutation = useMutation({
    mutationFn: registrarMovimiento,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['existencias'] });
      qc.invalidateQueries({ queryKey: ['movimientos'] });
      qc.invalidateQueries({ queryKey: ['kardex'] });
      qc.invalidateQueries({ queryKey: ['sugerencias-compra'] });
      setCantidad('');
      setReferencia('');
      setError(null);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'No se pudo registrar el movimiento.');
    },
  });

  const guardarMinimoMutation = useMutation({
    mutationFn: guardarStockMinimo,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['existencias'] });
      qc.invalidateQueries({ queryKey: ['sugerencias-compra'] });
    },
  });

  const eliminarMinimoMutation = useMutation({
    mutationFn: ({ hotelId: hid, productoId: pid }: { hotelId: number; productoId: number }) => eliminarStockMinimo(hid, pid),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['existencias'] });
      qc.invalidateQueries({ queryKey: ['sugerencias-compra'] });
    },
  });

  const eliminarMutation = useMutation({
    mutationFn: eliminarMovimiento,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['existencias'] });
      qc.invalidateQueries({ queryKey: ['movimientos'] });
      qc.invalidateQueries({ queryKey: ['kardex'] });
      qc.invalidateQueries({ queryKey: ['sugerencias-compra'] });
    },
  });

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    if (hotelId === '' || productoId === '' || unidadId === '') {
      setError('Completa hotel, producto y unidad.');
      return;
    }
    registrarMutation.mutate({
      tipo,
      fecha,
      hotelId: Number(hotelId),
      productoId: Number(productoId),
      unidadId: Number(unidadId),
      cantidad: Number(cantidad),
      referencia: referencia || undefined,
    });
  };

  const guardarMinimo = (productoIdGuardar: number) => {
    if (hotelId === '') return;
    const cantidadMinimaBase = Number(minimosEdit[productoIdGuardar]);
    if (!cantidadMinimaBase || cantidadMinimaBase <= 0) return;
    guardarMinimoMutation.mutate({
      hotelId: Number(hotelId),
      productoId: productoIdGuardar,
      cantidadMinimaBase,
    });
  };

  const limpiarMinimo = (productoIdEliminar: number) => {
    if (hotelId === '') return;
    setMinimosEdit((prev) => ({ ...prev, [productoIdEliminar]: '' }));
    eliminarMinimoMutation.mutate({ hotelId: Number(hotelId), productoId: productoIdEliminar });
  };

  const crearDocumentoDesdeSugerencia = () => {
    if (hotelId === '' || !sugerencias?.length) return;

    const proveedorIdSugerido = sugerencias.find((s) => s.proveedorId != null)?.proveedorId ?? null;
    navigate('/documentos', {
      state: {
        sugerenciaCompra: {
          hotelId: Number(hotelId),
          proveedorId: proveedorIdSugerido,
          observaciones: `Generado desde sugerencia de compra para ${hotelSeleccionado?.nombre ?? 'hotel seleccionado'}.`,
          lineas: sugerencias.map((s) => ({
            productoId: s.productoId,
            cantidadSugeridaBase: s.cantidadSugeridaBase,
            ultimoPrecioBase: s.ultimoPrecioBase,
          })),
        },
      },
    });
  };

  const exportarKardex = async () => {
    if (hotelId === '' || kardexProductoId === '') return;
    setKardexDescargando(true);
    setKardexError(null);
    try {
      await descargarKardexExcel({
        hotelId: Number(hotelId),
        productoId: Number(kardexProductoId),
        desde: kardexDesde || undefined,
        hasta: kardexHasta || undefined,
      });
    } catch {
      setKardexError('No se pudo exportar el Kardex.');
    } finally {
      setKardexDescargando(false);
    }
  };

  const colSpanMovimientos = esAdminOGerencia ? 7 : 6;
  const colSpanExistencias = esAdminOGerencia ? 10 : 9;

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(16,185,129,0.25),rgba(15,23,42,0)_58%)]" />
          <div className="relative flex flex-wrap items-end justify-between gap-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-emerald-300/80">Operación</p>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Inventario</h1>
              <p className="mt-1 text-sm text-slate-300">
                {hotelSeleccionado ? `${hotelSeleccionado.nombre}: existencias, mínimos y movimientos.` : 'Selecciona un hotel para ver existencias.'}
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

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <KpiInventario titulo="Productos visibles" valor={String(resumen.productos)} detalle="actividad o mínimo" />
        <KpiInventario titulo="Bajo mínimo" valor={String(resumen.bajoMinimo)} detalle="requieren compra" tono={resumen.bajoMinimo > 0 ? 'warn' : 'ok'} />
        <KpiInventario titulo="Stock negativo" valor={String(resumen.negativos)} detalle="requieren revisión" tono={resumen.negativos > 0 ? 'bad' : 'ok'} />
        <KpiInventario titulo="Stock en cero" valor={String(resumen.sinStock)} detalle="sin existencia disponible" tono={resumen.sinStock > 0 ? 'warn' : 'ok'} />
        <KpiInventario titulo="Mínimos configurados" valor={String(resumen.configurados)} detalle="hotel/producto" />
      </div>

      <form onSubmit={enviar} className="card card-pad">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="card-title">Registrar movimiento</h2>
            <p className="mt-1 text-xs text-slate-500">Salidas, mermas, ajustes y entradas manuales se reflejan al instante.</p>
          </div>
          {tipo === 'Ajuste' && <span className="badge-amber">Permite cantidad negativa</span>}
        </div>
        {error && (
          <div className="mb-3 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm text-rose-700">{error}</div>
        )}
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-12 xl:items-end">
          <div className="xl:col-span-2">
            <label className="label">Tipo</label>
            <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="field">
              {TIPOS.map((t) => (
                <option key={t.valor} value={t.valor}>
                  {t.etiqueta}
                </option>
              ))}
            </select>
          </div>
          <div className="xl:col-span-2">
            <label className="label">Fecha</label>
            <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} required className="field" />
          </div>
          <div className="xl:col-span-3">
            <label className="label">Producto</label>
            <select
              value={productoId}
              onChange={(e) => void seleccionarProducto(e.target.value === '' ? '' : Number(e.target.value))}
              required
              className="field"
            >
              <option value="">Selecciona…</option>
              {productos?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.nombre}
                </option>
              ))}
            </select>
          </div>
          <div className="xl:col-span-2">
            <label className="label">Unidad</label>
            <select
              value={unidadId}
              onChange={(e) => setUnidadId(e.target.value === '' ? '' : Number(e.target.value))}
              required
              disabled={productoId === ''}
              className="field"
            >
              <option value="">—</option>
              {conversiones.map((c) => (
                <option key={c.unidadId} value={c.unidadId}>
                  {c.unidadNombre}
                </option>
              ))}
            </select>
          </div>
          <div className="xl:col-span-3">
            <label className="label">Cantidad</label>
            <input
              type="number"
              step="0.01"
              value={cantidad}
              onChange={(e) => setCantidad(e.target.value)}
              required
              className="field"
            />
          </div>
          <div className="md:col-span-2 xl:col-span-9">
            <label className="label">Referencia</label>
            <input
              value={referencia}
              onChange={(e) => setReferencia(e.target.value)}
              placeholder="cocina, conteo, ajuste…"
              className="field"
            />
          </div>
          <div className="md:col-span-2 xl:col-span-3">
            <button type="submit" disabled={registrarMutation.isPending} className="btn-primary w-full justify-center whitespace-nowrap">
              {registrarMutation.isPending ? 'Guardando…' : 'Registrar movimiento'}
            </button>
          </div>
        </div>
      </form>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h3 className="card-title">Compra sugerida</h3>
            <p className="mt-1 text-xs text-slate-500">Calculada con faltantes contra mínimos y último precio conocido.</p>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-3 text-right">
            <div>
              <div className="text-xs text-slate-500">Costo estimado</div>
              <div className="text-lg font-semibold text-slate-900">{Q(totalSugerido)}</div>
            </div>
            <button
              type="button"
              onClick={crearDocumentoDesdeSugerencia}
              disabled={!sugerencias?.length}
              className="btn-primary btn-sm"
            >
              Crear documento
            </button>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Producto</th>
                <th className="th text-right">Comprar</th>
                <th className="th text-right">Existencia</th>
                <th className="th text-right">Mínimo</th>
                <th className="th">Proveedor sugerido</th>
                <th className="th text-right">Último precio</th>
                <th className="th text-right">Costo est.</th>
              </tr>
            </thead>
            <tbody>
              {sugerencias?.map((s) => (
                <tr key={s.productoId} className="trow">
                  <td className="td">
                    <div className="font-medium text-slate-800">{s.producto}</div>
                    <div className="text-xs text-slate-400">{s.categoria}</div>
                  </td>
                  <td className="td text-right font-semibold text-amber-700">
                    {numero(s.cantidadSugeridaBase)} {s.unidadBase}
                  </td>
                  <td className="td text-right text-slate-600">{numero(s.existencia)} {s.unidadBase}</td>
                  <td className="td text-right text-slate-600">{numero(s.stockMinimo)} {s.unidadBase}</td>
                  <td className="td text-slate-600">
                    {s.proveedorNombre ? (
                      <>
                        <div className="font-medium text-slate-700">{s.proveedorNombre}</div>
                        <div className="text-xs text-slate-400">última compra: {s.ultimaCompra ?? '—'}</div>
                      </>
                    ) : (
                      <span className="text-slate-400">Sin historial</span>
                    )}
                  </td>
                  <td className="td text-right text-slate-600">
                    {s.ultimoPrecioBase != null ? `${Q(s.ultimoPrecioBase)}/${s.unidadBase}` : '—'}
                  </td>
                  <td className="td text-right font-semibold text-slate-900">
                    {s.costoEstimado != null ? Q(s.costoEstimado) : '—'}
                  </td>
                </tr>
              ))}
              {sugerencias?.length === 0 && (
                <tr>
                  <td colSpan={7} className="empty-cell">No hay compras sugeridas para este hotel.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(220px,1.4fr)_minmax(180px,0.8fr)_auto] lg:items-end">
          <div>
            <label className="label">Buscar producto</label>
            <input
              value={filtroTexto}
              onChange={(e) => setFiltroTexto(e.target.value)}
              placeholder="Nombre o categoría"
              className="field"
            />
          </div>
          <div>
            <label className="label">Categoría</label>
            <select value={filtroCategoria} onChange={(e) => setFiltroCategoria(e.target.value)} className="field">
              <option value="">Todas</option>
              {categorias.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>
          <label className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-600">
            <input type="checkbox" checked={soloRiesgo} onChange={(e) => setSoloRiesgo(e.target.checked)} className="h-4 w-4 accent-emerald-600" />
            Solo riesgo
          </label>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h3 className="card-title">Kardex del producto</h3>
            <p className="mt-1 text-xs text-slate-500">Entradas por compra, movimientos manuales y saldo acumulado en unidad base.</p>
          </div>
          <div className="flex flex-wrap items-center justify-end gap-2">
            {kardexCargando && <span className="badge-slate">Actualizando</span>}
            <button
              type="button"
              onClick={() => void exportarKardex()}
              disabled={hotelId === '' || kardexProductoId === '' || kardexDescargando}
              className="btn-secondary btn-sm"
            >
              {kardexDescargando ? 'Exportando...' : 'Exportar Excel'}
            </button>
          </div>
        </div>
        {kardexError && <div className="border-b border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{kardexError}</div>}
        <div className="border-b border-slate-200 bg-slate-50/70 px-4 py-4">
          <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_150px_auto] lg:items-end">
            <div>
              <label className="label">Producto</label>
              <select
                value={kardexProductoId}
                onChange={(e) => setKardexProductoId(e.target.value === '' ? '' : Number(e.target.value))}
                className="field bg-white"
              >
                <option value="">Selecciona producto</option>
                {productos?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.nombre}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label">Desde</label>
              <input type="date" value={kardexDesde} onChange={(e) => setKardexDesde(e.target.value)} className="field bg-white" />
            </div>
            <div>
              <label className="label">Hasta</label>
              <input type="date" value={kardexHasta} onChange={(e) => setKardexHasta(e.target.value)} className="field bg-white" />
            </div>
            <button
              type="button"
              onClick={() => {
                setKardexDesde('');
                setKardexHasta('');
              }}
              className="btn-secondary"
            >
              Limpiar fechas
            </button>
          </div>
        </div>
        {kardex ? (
          <>
            <div className="grid grid-cols-2 gap-3 border-b border-slate-200 px-4 py-4 md:grid-cols-5">
              <div>
                <div className="text-xs text-slate-400">Saldo inicial</div>
                <div className="mt-1 font-semibold text-slate-900">{numero(kardex.saldoInicial)} {kardex.unidadBase}</div>
              </div>
              <div>
                <div className="text-xs text-slate-400">Entradas</div>
                <div className="mt-1 font-semibold text-emerald-700">{numero(kardex.totalEntradas)} {kardex.unidadBase}</div>
              </div>
              <div>
                <div className="text-xs text-slate-400">Salidas</div>
                <div className="mt-1 font-semibold text-rose-700">{numero(kardex.totalSalidas)} {kardex.unidadBase}</div>
              </div>
              <div>
                <div className="text-xs text-slate-400">Ajustes</div>
                <div className="mt-1 font-semibold text-amber-700">{numero(kardex.totalAjustes)} {kardex.unidadBase}</div>
              </div>
              <div>
                <div className="text-xs text-slate-400">Saldo final</div>
                <div className="mt-1 font-semibold text-slate-900">{numero(kardex.saldoFinal)} {kardex.unidadBase}</div>
              </div>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="thead">
                  <tr>
                    <th className="th">Fecha</th>
                    <th className="th">Tipo</th>
                    <th className="th">Referencia</th>
                    <th className="th text-right">Entrada</th>
                    <th className="th text-right">Salida</th>
                    <th className="th text-right">Ajuste</th>
                    <th className="th text-right">Saldo</th>
                    <th className="th text-right">Costo</th>
                  </tr>
                </thead>
                <tbody>
                  {kardex.movimientos.map((m) => (
                    <tr key={m.id} className="trow">
                      <td className="td whitespace-nowrap text-slate-600">{m.fecha}</td>
                      <td className="td">
                        <span className={BADGE_TIPO[m.tipo] ?? 'badge-slate'}>{m.tipo}</span>
                      </td>
                      <td className="td">
                        <div className="font-medium text-slate-700">{m.referencia}</div>
                        <div className="text-xs text-slate-400">
                          {m.proveedor ?? m.documento ?? m.creadoPor ?? 'Sin detalle'}
                        </div>
                      </td>
                      <td className="td text-right text-emerald-700">
                        {m.entrada ? `${numero(m.entrada)} ${kardex.unidadBase}` : '-'}
                      </td>
                      <td className="td text-right text-rose-700">
                        {m.salida ? `${numero(m.salida)} ${kardex.unidadBase}` : '-'}
                      </td>
                      <td className="td text-right text-amber-700">
                        {m.ajuste ? `${numero(m.ajuste)} ${kardex.unidadBase}` : '-'}
                      </td>
                      <td className="td text-right font-semibold text-slate-900">{numero(m.saldo)} {kardex.unidadBase}</td>
                      <td className="td text-right text-slate-500">
                        {m.costoTotal != null ? (
                          <>
                            <div>{Q(m.costoTotal)}</div>
                            {m.costoUnitario != null && <div className="text-xs text-slate-400">{Q(m.costoUnitario)}/{kardex.unidadBase}</div>}
                          </>
                        ) : (
                          '-'
                        )}
                      </td>
                    </tr>
                  ))}
                  {kardex.movimientos.length === 0 && (
                    <tr>
                      <td colSpan={8} className="empty-cell">Sin movimientos para este producto y rango.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        ) : (
          <div className="empty-cell">Selecciona un producto para ver su Kardex.</div>
        )}
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h3 className="card-title">Existencias y mínimos</h3>
            <p className="mt-1 text-xs text-slate-500">{existenciasFiltradas.length} productos visibles.</p>
          </div>
          {isLoading && <span className="badge-slate">Cargando</span>}
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Producto</th>
                <th className="th">Categoría</th>
                <th className="th text-right">Comprado</th>
                <th className="th text-right">Salidas</th>
                <th className="th text-right">Mermas</th>
                <th className="th text-right">Mínimo</th>
                {esAdminOGerencia && <th className="th text-right">Configurar</th>}
                <th className="th text-right">Existencia</th>
                <th className="th text-right">Faltante</th>
                <th className="th text-right">Estado</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={colSpanExistencias} className="empty-cell">Cargando existencias…</td>
                </tr>
              )}
              {!isLoading &&
                existenciasFiltradas.map((e) => (
                  <tr key={e.productoId} className="trow">
                    <td className="td">
                      <div className="font-medium text-slate-700">{e.producto}</div>
                      <button
                        type="button"
                        onClick={() => setKardexProductoId(e.productoId)}
                        className="mt-1 text-xs font-medium text-emerald-700 hover:text-emerald-900"
                      >
                        Ver kardex
                      </button>
                    </td>
                    <td className="td text-slate-500">{e.categoria}</td>
                    <td className="td text-right text-slate-600">{numero(e.comprado)}</td>
                    <td className="td text-right text-slate-600">{numero(e.salidas)}</td>
                    <td className="td text-right text-slate-600">{numero(e.mermas)}</td>
                    <td className="td text-right text-slate-600">
                      {e.stockMinimo > 0 ? `${numero(e.stockMinimo)} ${e.unidadBase}` : '—'}
                    </td>
                    {esAdminOGerencia && (
                      <td className="td">
                        <div className="flex justify-end gap-1">
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            value={minimosEdit[e.productoId] ?? ''}
                            onChange={(ev) => setMinimosEdit((prev) => ({ ...prev, [e.productoId]: ev.target.value }))}
                            className="field w-24 py-1.5 text-right"
                            placeholder="0"
                          />
                          <button
                            type="button"
                            onClick={() => guardarMinimo(e.productoId)}
                            disabled={guardarMinimoMutation.isPending || !(Number(minimosEdit[e.productoId]) > 0)}
                            className="btn-secondary btn-sm"
                          >
                            Guardar
                          </button>
                          {e.stockMinimo > 0 && (
                            <button type="button" onClick={() => limpiarMinimo(e.productoId)} className="btn-secondary btn-sm">
                              Quitar
                            </button>
                          )}
                        </div>
                      </td>
                    )}
                    <td className="td text-right">
                      <span className={e.existencia < 0 ? 'badge-red' : e.existencia === 0 ? 'badge-amber' : 'badge-green'}>
                        {numero(e.existencia)} {e.unidadBase}
                      </span>
                    </td>
                    <td className={`td text-right font-semibold ${e.faltante > 0 ? 'text-amber-700' : 'text-slate-500'}`}>
                      {e.faltante > 0 ? `${numero(e.faltante)} ${e.unidadBase}` : '—'}
                    </td>
                    <td className="td text-right">
                      <span className={badgeEstado(e)}>{textoEstado(e.estadoStock)}</span>
                    </td>
                  </tr>
                ))}
              {!isLoading && existenciasFiltradas.length === 0 && (
                <tr>
                  <td colSpan={colSpanExistencias} className="empty-cell">Sin productos para los filtros seleccionados.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h3 className="card-title">Movimientos recientes</h3>
            <p className="mt-1 text-xs text-slate-500">Últimos movimientos registrados para el hotel seleccionado.</p>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Fecha</th>
                <th className="th">Tipo</th>
                <th className="th">Producto</th>
                <th className="th text-right">Cantidad base</th>
                <th className="th">Referencia</th>
                <th className="th">Registró</th>
                {esAdminOGerencia && <th className="th"></th>}
              </tr>
            </thead>
            <tbody>
              {movimientos?.map((m) => (
                <tr key={m.id} className="trow">
                  <td className="td whitespace-nowrap text-slate-600">{m.fecha}</td>
                  <td className="td">
                    <span className={BADGE_TIPO[m.tipo] ?? 'badge-slate'}>{m.tipo}</span>
                  </td>
                  <td className="td font-medium text-slate-700">{m.producto}</td>
                  <td className="td text-right text-slate-600">
                    {numero(m.cantidadBase)} {m.unidadBase}
                  </td>
                  <td className="td text-slate-500">{m.referencia ?? '—'}</td>
                  <td className="td text-slate-500">{m.creadoPor ?? '—'}</td>
                  {esAdminOGerencia && (
                    <td className="td text-right">
                      <button
                        onClick={() => eliminarMutation.mutate(m.id)}
                        className="rounded-md px-2 py-1 text-xs font-medium text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
                        title="Revertir movimiento"
                      >
                        Revertir
                      </button>
                    </td>
                  )}
                </tr>
              ))}
              {movimientos?.length === 0 && (
                <tr>
                  <td colSpan={colSpanMovimientos} className="empty-cell">Sin movimientos registrados.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
