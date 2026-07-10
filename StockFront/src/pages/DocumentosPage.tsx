import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  actualizarDocumento,
  anularDocumento,
  crearDocumento,
  eliminarDocumento,
  listarDocumentos,
  obtenerDocumento,
  recibirDocumento,
} from '../features/compras/comprasApi';
import type { CrearDocumentoCompraRequest, EstadoDocumentoCompra, LineaNueva } from '../features/compras/types';
import { listarConversiones, listarHoteles, listarProductos, listarProveedores } from '../features/catalogos/catalogosApi';
import type { Conversion } from '../features/catalogos/types';
import { useAuth } from '../features/auth/authStore';
import type { SugerenciaCompra } from '../features/inventario/inventarioApi';

function lineaVacia(): LineaNueva {
  return { productoId: '', unidadId: '', cantidad: '', precioUnitario: '' };
}

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const fechaCorta = (fecha: string) =>
  new Intl.DateTimeFormat('es-GT', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${fecha}T00:00:00`));

const fechaInput = (fecha: Date) => {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
};

const rangoMesActual = () => {
  const hoy = new Date();
  return {
    desde: fechaInput(new Date(hoy.getFullYear(), hoy.getMonth(), 1)),
    hasta: fechaInput(new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0)),
  };
};

function KpiDocumento({ titulo, valor, detalle }: { titulo: string; valor: string; detalle: string }) {
  return (
    <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-400">{titulo}</div>
      <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">{valor}</div>
      <div className="mt-1 text-xs text-slate-500">{detalle}</div>
    </div>
  );
}

function badgeEstadoDocumento(estado: EstadoDocumentoCompra) {
  if (estado === 'Recibido') return 'badge-green';
  if (estado === 'Borrador') return 'badge-amber';
  return 'badge-slate';
}

type EstadoNavegacionDocumentos = {
  sugerenciaCompra?: {
    hotelId: number;
    proveedorId: number | null;
    observaciones?: string;
    lineas: Pick<SugerenciaCompra, 'productoId' | 'cantidadSugeridaBase' | 'ultimoPrecioBase'>[];
  };
};

export function DocumentosPage() {
  const qc = useQueryClient();
  const location = useLocation();
  const navigate = useNavigate();
  const precargaAplicada = useRef(false);
  const puedeEliminar = useAuth((s) => s.tieneRol('Admin', 'Gerencia'));

  const invalidarFlujoCompras = () => {
    qc.invalidateQueries({ queryKey: ['documentos'] });
    qc.invalidateQueries({ queryKey: ['existencias'] });
    qc.invalidateQueries({ queryKey: ['sugerencias-compra'] });
    qc.invalidateQueries({ queryKey: ['alertas-stock'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  };

  const [filtroTexto, setFiltroTexto] = useState('');
  const [filtroHotelId, setFiltroHotelId] = useState<number | ''>('');
  const [filtroDesde, setFiltroDesde] = useState('');
  const [filtroHasta, setFiltroHasta] = useState('');

  const filtrosServidor = useMemo(
    () => ({
      hotelId: filtroHotelId === '' ? undefined : filtroHotelId,
      desde: filtroDesde || undefined,
      hasta: filtroHasta || undefined,
    }),
    [filtroHotelId, filtroDesde, filtroHasta],
  );

  const eliminarMutation = useMutation({
    mutationFn: eliminarDocumento,
    onSuccess: invalidarFlujoCompras,
  });

  const recibirMutation = useMutation({
    mutationFn: recibirDocumento,
    onSuccess: invalidarFlujoCompras,
  });

  const anularMutation = useMutation({
    mutationFn: anularDocumento,
    onSuccess: invalidarFlujoCompras,
  });

  const confirmarEliminar = (id: number, numero: string) => {
    if (window.confirm(`¿Eliminar el documento ${numero}? Esta acción no se puede deshacer.`)) {
      eliminarMutation.mutate(id);
    }
  };

  const confirmarRecibir = (id: number) => {
    recibirMutation.mutate(id);
  };

  const confirmarAnular = (id: number, numero: string) => {
    if (window.confirm(`¿Anular el documento ${numero}? Dejará de sumar al inventario y reportes.`)) {
      anularMutation.mutate(id);
    }
  };

  const { data: documentos, isLoading } = useQuery({
    queryKey: ['documentos', filtrosServidor],
    queryFn: () => listarDocumentos(filtrosServidor),
  });
  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: proveedores } = useQuery({ queryKey: ['proveedores'], queryFn: () => listarProveedores(true) });
  const { data: productos } = useQuery({ queryKey: ['productos'], queryFn: () => listarProductos(true) });

  const documentosFiltrados = useMemo(() => {
    const texto = filtroTexto.trim().toLowerCase();
    if (!texto) return documentos ?? [];
    return (documentos ?? []).filter((d) =>
      [d.numeroDocumento, d.hotelNombre, d.proveedorNombre].some((v) => v.toLowerCase().includes(texto)),
    );
  }, [documentos, filtroTexto]);

  const resumen = useMemo(() => {
    const recibidos = documentosFiltrados.filter((d) => d.estado === 'Recibido');
    const total = recibidos.reduce((acc, d) => acc + d.total, 0);
    const promedio = recibidos.length ? total / recibidos.length : 0;
    const borradores = documentosFiltrados.filter((d) => d.estado === 'Borrador').length;
    const anulados = documentosFiltrados.filter((d) => d.estado === 'Anulado').length;
    return { total, promedio, borradores, anulados };
  }, [documentosFiltrados]);

  const [mostrarForm, setMostrarForm] = useState(false);
  const [editandoId, setEditandoId] = useState<number | null>(null);
  const [fecha, setFecha] = useState(() => fechaInput(new Date()));
  const [numeroDocumento, setNumeroDocumento] = useState('');
  const [hotelId, setHotelId] = useState<number | ''>('');
  const [proveedorId, setProveedorId] = useState<number | ''>('');
  const [estado, setEstado] = useState<EstadoDocumentoCompra>('Recibido');
  const [retencion, setRetencion] = useState('0');
  const [observaciones, setObservaciones] = useState('');
  const [lineas, setLineas] = useState<LineaNueva[]>([lineaVacia()]);
  const [conversionesPorProducto, setConversionesPorProducto] = useState<Record<number, Conversion[]>>({});
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const cerrarForm = () => {
    setMostrarForm(false);
    setEditandoId(null);
    setFecha(fechaInput(new Date()));
    setNumeroDocumento('');
    setHotelId('');
    setProveedorId('');
    setEstado('Recibido');
    setObservaciones('');
    setRetencion('0');
    setLineas([lineaVacia()]);
    setError(null);
    setAviso(null);
  };

  const abrirNuevo = () => {
    cerrarForm();
    setMostrarForm(true);
  };

  const crearMutation = useMutation({
    mutationFn: (payload: CrearDocumentoCompraRequest) =>
      editandoId ? actualizarDocumento(editandoId, payload) : crearDocumento(payload),
    onSuccess: () => {
      invalidarFlujoCompras();
      cerrarForm();
    },
    onError: (e: unknown) => {
      const msg =
        (e as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'No se pudo guardar el documento.';
      setError(msg);
    },
  });

  const editar = async (id: number) => {
    const doc = await obtenerDocumento(id);
    await Promise.all(
      [...new Set(doc.detalles.map((l) => l.productoId))].map(async (pid) => {
        const conv = await listarConversiones(pid);
        setConversionesPorProducto((prev) => ({ ...prev, [pid]: conv }));
      }),
    );
    setEditandoId(doc.id);
    setFecha(doc.fecha);
    setNumeroDocumento(doc.numeroDocumento);
    setHotelId(doc.hotelId);
    setProveedorId(doc.proveedorId);
    setEstado(doc.estado);
    setRetencion(String(doc.retencion));
    setObservaciones(doc.observaciones ?? '');
    setLineas(
      doc.detalles.map((l) => ({
        productoId: l.productoId,
        unidadId: l.unidadId,
        cantidad: String(l.cantidad),
        precioUnitario: String(l.precioUnitario),
      })),
    );
    setError(null);
    setAviso(null);
    setMostrarForm(true);
  };

  const cargarConversiones = async (productoId: number) => {
    if (conversionesPorProducto[productoId]) return;
    const conv = await listarConversiones(productoId);
    setConversionesPorProducto((prev) => ({ ...prev, [productoId]: conv }));
  };

  useEffect(() => {
    const sugerencia = (location.state as EstadoNavegacionDocumentos | null)?.sugerenciaCompra;
    if (precargaAplicada.current || !sugerencia || !productos || !proveedores) return;

    const lineasPrecargadas = sugerencia.lineas
      .map<LineaNueva>((linea) => {
        const producto = productos.find((p) => p.id === linea.productoId);
        return {
          productoId: linea.productoId,
          unidadId: producto?.unidadBaseId ?? '',
          cantidad: String(linea.cantidadSugeridaBase),
          precioUnitario: linea.ultimoPrecioBase != null ? String(linea.ultimoPrecioBase) : '',
        };
      })
      .filter((linea) => linea.unidadId !== '');

    if (lineasPrecargadas.length === 0) return;

    const proveedorActivo =
      sugerencia.proveedorId != null && proveedores.some((p) => p.id === sugerencia.proveedorId)
        ? sugerencia.proveedorId
        : '';

    precargaAplicada.current = true;
    setMostrarForm(true);
    setEditandoId(null);
    setFecha(fechaInput(new Date()));
    setNumeroDocumento('');
    setHotelId(sugerencia.hotelId);
    setProveedorId(proveedorActivo);
    setEstado('Borrador');
    setRetencion('0');
    setObservaciones(sugerencia.observaciones ?? 'Generado desde sugerencia de compra.');
    setLineas(lineasPrecargadas);
    setError(null);
    setAviso(
      proveedorActivo
        ? 'Se precargaron los productos sugeridos. Revisa cantidades, precios y número de documento antes de guardar.'
        : 'Se precargaron los productos sugeridos. Selecciona un proveedor y revisa precios antes de guardar.',
    );

    void Promise.all(
      [...new Set(lineasPrecargadas.map((linea) => Number(linea.productoId)))].map(async (productoId) => ({
        productoId,
        conversiones: await listarConversiones(productoId),
      })),
    ).then((resultados) => {
      setConversionesPorProducto((prev) => {
        const next = { ...prev };
        for (const resultado of resultados) next[resultado.productoId] = resultado.conversiones;
        return next;
      });
    });

    navigate('/documentos', { replace: true, state: null });
  }, [location.state, navigate, productos, proveedores]);

  const actualizarLinea = (idx: number, cambios: Partial<LineaNueva>) => {
    setLineas((prev) => prev.map((l, i) => (i === idx ? { ...l, ...cambios } : l)));
  };

  const seleccionarProducto = (idx: number, productoId: number | '') => {
    actualizarLinea(idx, { productoId, unidadId: '' });
    if (productoId !== '') void cargarConversiones(productoId);
  };

  const totalDocumento = useMemo(
    () =>
      lineas.reduce((acc, l) => {
        const cant = Number(l.cantidad) || 0;
        const precio = Number(l.precioUnitario) || 0;
        return acc + cant * precio;
      }, 0),
    [lineas],
  );

  const productosRepetidos = useMemo(() => {
    const vistos = new Set<number>();
    const repetidos = new Set<number>();
    for (const linea of lineas) {
      if (linea.productoId === '') continue;
      if (vistos.has(linea.productoId)) repetidos.add(linea.productoId);
      vistos.add(linea.productoId);
    }
    return repetidos;
  }, [lineas]);

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!hotelId || !proveedorId) return setError('Selecciona hotel y proveedor.');
    const detalles = lineas
      .filter((l) => l.productoId && l.unidadId && l.cantidad && l.precioUnitario)
      .map((l) => ({
        productoId: Number(l.productoId),
        unidadId: Number(l.unidadId),
        cantidad: Number(l.cantidad),
        precioUnitario: Number(l.precioUnitario),
      }));

    if (detalles.length === 0) return setError('Agrega al menos un producto con cantidad y precio.');

    crearMutation.mutate({
      fecha,
      numeroDocumento,
      hotelId: Number(hotelId),
      proveedorId: Number(proveedorId),
      estado,
      retencion: Number(retencion) || 0,
      observaciones: observaciones || undefined,
      detalles,
    });
  };

  const limpiarFiltros = () => {
    setFiltroTexto('');
    setFiltroHotelId('');
    setFiltroDesde('');
    setFiltroHasta('');
  };

  const usarMesActual = () => {
    const rango = rangoMesActual();
    setFiltroDesde(rango.desde);
    setFiltroHasta(rango.hasta);
  };

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(16,185,129,0.28),rgba(15,23,42,0)_58%)]" />
          <div className="relative flex flex-wrap items-center justify-between gap-4">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-emerald-300/80">Compras</p>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Documentos de compra</h1>
              <p className="mt-1 max-w-2xl text-sm text-slate-300">
                Registra ingresos, controla proveedores y detecta variaciones antes de que lleguen al inventario.
              </p>
            </div>
            <button onClick={abrirNuevo} className="btn bg-white text-slate-900 shadow-sm hover:bg-emerald-50">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
              </svg>
              Nuevo documento
            </button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <KpiDocumento titulo="Total recibido" valor={Q(resumen.total)} detalle="solo documentos recibidos" />
        <KpiDocumento titulo="Promedio recibido" valor={Q(resumen.promedio)} detalle="por documento recibido" />
        <KpiDocumento titulo="Pendientes" valor={String(resumen.borradores)} detalle={`${resumen.anulados} anulados en la vista`} />
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(220px,1.5fr)_minmax(180px,1fr)_150px_150px_auto] lg:items-end">
          <div>
            <label className="label">Buscar</label>
            <input
              value={filtroTexto}
              onChange={(e) => setFiltroTexto(e.target.value)}
              placeholder="Documento, hotel o proveedor"
              className="field"
            />
          </div>
          <div>
            <label className="label">Hotel</label>
            <select
              value={filtroHotelId}
              onChange={(e) => setFiltroHotelId(e.target.value === '' ? '' : Number(e.target.value))}
              className="field"
            >
              <option value="">Todos</option>
              {hoteles?.map((h) => (
                <option key={h.id} value={h.id}>
                  {h.nombre}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Desde</label>
            <input type="date" value={filtroDesde} onChange={(e) => setFiltroDesde(e.target.value)} className="field" />
          </div>
          <div>
            <label className="label">Hasta</label>
            <input type="date" value={filtroHasta} onChange={(e) => setFiltroHasta(e.target.value)} className="field" />
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={usarMesActual} className="btn-secondary">
              Mes actual
            </button>
            <button type="button" onClick={limpiarFiltros} className="btn-secondary">
              Limpiar
            </button>
          </div>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h2 className="card-title">Historial de documentos</h2>
            <p className="mt-1 text-xs text-slate-500">Últimos registros según filtros activos.</p>
          </div>
          {isLoading && <span className="badge-slate">Cargando</span>}
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Fecha</th>
                <th className="th">Documento</th>
                <th className="th">Hotel</th>
                <th className="th">Proveedor</th>
                <th className="th">Estado</th>
                <th className="th text-right">Total</th>
                <th className="th text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={7} className="empty-cell">Cargando documentos…</td>
                </tr>
              )}
              {!isLoading &&
                documentosFiltrados.map((d) => (
                  <tr key={d.id} className="trow">
                    <td className="td whitespace-nowrap text-slate-600">{fechaCorta(d.fecha)}</td>
                    <td className="td">
                      <span className="font-semibold text-slate-800">{d.numeroDocumento}</span>
                    </td>
                    <td className="td text-slate-600">{d.hotelNombre}</td>
                    <td className="td text-slate-600">{d.proveedorNombre}</td>
                    <td className="td">
                      <span className={badgeEstadoDocumento(d.estado)}>{d.estado}</span>
                    </td>
                    <td className={`td text-right font-semibold ${d.estado === 'Anulado' ? 'text-slate-400 line-through' : 'text-slate-900'}`}>
                      {Q(d.total)}
                    </td>
                    <td className="td whitespace-nowrap text-right">
                      {d.estado !== 'Anulado' && (
                        <button
                          onClick={() => void editar(d.id)}
                          className="mr-1 rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                          title="Editar documento"
                        >
                          Editar
                        </button>
                      )}
                      {d.estado === 'Borrador' && (
                        <button
                          onClick={() => confirmarRecibir(d.id)}
                          disabled={recibirMutation.isPending}
                          className="mr-1 rounded-md px-2 py-1 text-xs font-medium text-emerald-600 transition-colors hover:bg-emerald-50"
                          title="Recibir documento"
                        >
                          Recibir
                        </button>
                      )}
                      {puedeEliminar && d.estado !== 'Anulado' && (
                        <button
                          onClick={() => confirmarAnular(d.id, d.numeroDocumento)}
                          disabled={anularMutation.isPending}
                          className="mr-1 rounded-md px-2 py-1 text-xs font-medium text-amber-600 transition-colors hover:bg-amber-50"
                          title="Anular documento"
                        >
                          Anular
                        </button>
                      )}
                      {puedeEliminar && (
                        <button
                          onClick={() => confirmarEliminar(d.id, d.numeroDocumento)}
                          className="rounded-md px-2 py-1 text-xs font-medium text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
                          title="Eliminar documento"
                        >
                          Eliminar
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              {!isLoading && documentosFiltrados.length === 0 && (
                <tr>
                  <td colSpan={7} className="empty-cell">
                    No hay documentos con estos filtros. Ajusta la búsqueda o registra una compra nueva.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {mostrarForm && (
        <div className="fixed inset-0 z-50">
          <div className="absolute inset-0 bg-slate-950/50 backdrop-blur-sm" onClick={cerrarForm} />
          <form
            onSubmit={enviar}
            className="absolute right-0 top-0 flex h-full w-full max-w-5xl flex-col bg-white shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-4 sm:px-6">
              <div>
                <p className="text-xs font-medium uppercase tracking-wide text-emerald-600">
                  {editandoId ? 'Edición de compra' : 'Registro de compra'}
                </p>
                <h2 className="mt-1 text-lg font-semibold text-slate-900">
                  {editandoId ? 'Editar documento' : 'Nuevo documento'}
                </h2>
              </div>
              <button
                type="button"
                onClick={cerrarForm}
                className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
                aria-label="Cerrar"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-5 w-5">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div className="flex-1 overflow-y-auto px-5 py-5 sm:px-6">
              {error && (
                <div className="mb-4 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm text-rose-700">
                  {error}
                </div>
              )}
              {aviso && (
                <div className="mb-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-sm text-emerald-700">
                  {aviso}
                </div>
              )}

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
                <div>
                  <label className="label">Fecha *</label>
                  <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} required className="field" />
                </div>
                <div>
                  <label className="label">No. Documento *</label>
                  <input value={numeroDocumento} onChange={(e) => setNumeroDocumento(e.target.value)} required className="field" />
                </div>
                <div>
                  <label className="label">Hotel *</label>
                  <select
                    value={hotelId}
                    onChange={(e) => setHotelId(e.target.value === '' ? '' : Number(e.target.value))}
                    required
                    className="field"
                  >
                    <option value="">Selecciona…</option>
                    {hoteles?.map((h) => (
                      <option key={h.id} value={h.id}>
                        {h.nombre}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="label">Proveedor *</label>
                  <select
                    value={proveedorId}
                    onChange={(e) => setProveedorId(e.target.value === '' ? '' : Number(e.target.value))}
                    required
                    className="field"
                  >
                    <option value="">Selecciona…</option>
                    {proveedores?.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.nombre}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="label">Estado *</label>
                  <select
                    value={estado}
                    onChange={(e) => setEstado(e.target.value as EstadoDocumentoCompra)}
                    disabled={estado === 'Anulado'}
                    className="field"
                  >
                    <option value="Borrador">Borrador</option>
                    <option value="Recibido">Recibido</option>
                  </select>
                </div>
              </div>

              <div className="mt-6">
                <div className="mb-3 flex items-center justify-between gap-3">
                  <div>
                    <h3 className="card-title">Productos</h3>
                    <p className="mt-1 text-xs text-slate-500">Solo los documentos recibidos actualizan inventario y reportes.</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setLineas((prev) => [lineaVacia(), ...prev])}
                    className="btn-secondary btn-sm"
                  >
                    + Agregar línea
                  </button>
                </div>

                {productosRepetidos.size > 0 && (
                  <div className="mb-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                    Hay productos repetidos. Se puede guardar, pero conviene unirlos si pertenecen a la misma compra.
                  </div>
                )}

                <div className="space-y-2">
                  {lineas.map((linea, idx) => {
                    const conversiones = linea.productoId ? conversionesPorProducto[Number(linea.productoId)] : undefined;
                    const subtotal = (Number(linea.cantidad) || 0) * (Number(linea.precioUnitario) || 0);
                    const repetida = linea.productoId !== '' && productosRepetidos.has(linea.productoId);

                    return (
                      <div
                        key={idx}
                        className={`grid gap-2 rounded-xl border p-3 lg:grid-cols-[minmax(220px,2fr)_minmax(130px,1fr)_110px_120px_110px_40px] lg:items-end ${
                          repetida ? 'border-amber-200 bg-amber-50/50' : 'border-slate-200 bg-slate-50/50'
                        }`}
                      >
                        <div>
                          <label className="label">Producto</label>
                          <select
                            value={linea.productoId}
                            onChange={(e) => seleccionarProducto(idx, e.target.value === '' ? '' : Number(e.target.value))}
                            className="field bg-white"
                          >
                            <option value="">Producto…</option>
                            {productos?.map((p) => (
                              <option key={p.id} value={p.id}>
                                {p.nombre}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div>
                          <label className="label">Unidad</label>
                          <select
                            value={linea.unidadId}
                            onChange={(e) => actualizarLinea(idx, { unidadId: e.target.value === '' ? '' : Number(e.target.value) })}
                            disabled={!linea.productoId}
                            className="field bg-white"
                          >
                            <option value="">Unidad…</option>
                            {conversiones?.map((c) => (
                              <option key={c.unidadId} value={c.unidadId}>
                                {c.unidadNombre}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div>
                          <label className="label">Cantidad</label>
                          <input
                            type="number"
                            step="0.01"
                            min="0"
                            value={linea.cantidad}
                            onChange={(e) => actualizarLinea(idx, { cantidad: e.target.value })}
                            className="field bg-white"
                          />
                        </div>
                        <div>
                          <label className="label">Precio unit.</label>
                          <input
                            type="number"
                            step="0.01"
                            min="0"
                            value={linea.precioUnitario}
                            onChange={(e) => actualizarLinea(idx, { precioUnitario: e.target.value })}
                            className="field bg-white"
                          />
                        </div>
                        <div>
                          <label className="label">Subtotal</label>
                          <div className="rounded-lg bg-white px-3 py-2 text-right text-sm font-semibold text-slate-800 ring-1 ring-slate-200">
                            {Q(subtotal)}
                          </div>
                        </div>
                        <button
                          type="button"
                          onClick={() => setLineas((prev) => prev.filter((_, i) => i !== idx))}
                          disabled={lineas.length === 1}
                          className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600 disabled:opacity-30"
                          title="Eliminar línea"
                        >
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
                            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
                          </svg>
                        </button>
                      </div>
                    );
                  })}
                </div>
              </div>

              <div className="mt-6 grid grid-cols-1 gap-4 border-t border-slate-100 pt-5 lg:grid-cols-[140px_minmax(220px,1fr)]">
                <div>
                  <label className="label">Retención</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={retencion}
                    onChange={(e) => setRetencion(e.target.value)}
                    className="field"
                  />
                </div>
                <div>
                  <label className="label">Observaciones</label>
                  <input value={observaciones} onChange={(e) => setObservaciones(e.target.value)} className="field" />
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-5 py-4 sm:px-6">
              <div>
                <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Total documento</div>
                <div className="text-2xl font-semibold text-slate-900">{Q(totalDocumento)}</div>
              </div>
              <div className="flex gap-2">
                <button type="button" onClick={cerrarForm} className="btn-secondary">
                  Cancelar
                </button>
                <button type="submit" disabled={crearMutation.isPending} className="btn-primary">
                  {crearMutation.isPending ? 'Guardando…' : editandoId ? 'Actualizar documento' : 'Guardar documento'}
                </button>
              </div>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
