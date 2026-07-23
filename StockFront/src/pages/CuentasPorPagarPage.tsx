import { Fragment, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listarHoteles, listarProveedores } from '../features/catalogos/catalogosApi';
import {
  descargarCuentasPorPagarExcel,
  eliminarPagoProveedor,
  listarCuentasPorPagar,
  registrarPagoProveedor,
  type CuentaPorPagar,
  type EstadoCuentaPorPagar,
  type PagoProveedor,
  type RegistrarPagoProveedorRequest,
} from '../features/cuentasPorPagar/cuentasPorPagarApi';

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`;

const fechaInput = (fecha: Date) => {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
};

const fechaCorta = (fecha: string) =>
  new Intl.DateTimeFormat('es-GT', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${fecha}T00:00:00`));

function badgeEstado(estado: EstadoCuentaPorPagar) {
  if (estado === 'Vencido') return 'badge-red';
  if (estado === 'Parcial') return 'badge-amber';
  if (estado === 'Pagado') return 'badge-green';
  return 'badge-slate';
}

function Kpi({ titulo, valor, detalle }: { titulo: string; valor: string; detalle: string }) {
  return (
    <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-400">{titulo}</div>
      <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">{valor}</div>
      <div className="mt-1 text-xs text-slate-500">{detalle}</div>
    </div>
  );
}

export function CuentasPorPagarPage() {
  const qc = useQueryClient();
  const [hotelId, setHotelId] = useState<number | ''>('');
  const [proveedorId, setProveedorId] = useState<number | ''>('');
  const [desde, setDesde] = useState('');
  const [hasta, setHasta] = useState('');
  const [soloPendientes, setSoloPendientes] = useState(true);
  const [cuentaSeleccionada, setCuentaSeleccionada] = useState<CuentaPorPagar | null>(null);
  const [monto, setMonto] = useState('');
  const [fechaPago, setFechaPago] = useState(() => fechaInput(new Date()));
  const [metodoPago, setMetodoPago] = useState('Transferencia');
  const [referencia, setReferencia] = useState('');
  const [observaciones, setObservaciones] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [errorTabla, setErrorTabla] = useState<string | null>(null);
  const [cuentasAbiertas, setCuentasAbiertas] = useState<Record<number, boolean>>({});
  const [descargandoExcel, setDescargandoExcel] = useState(false);

  const filtros = useMemo(
    () => ({
      hotelId: hotelId === '' ? undefined : hotelId,
      proveedorId: proveedorId === '' ? undefined : proveedorId,
      desde: desde || undefined,
      hasta: hasta || undefined,
      soloPendientes,
    }),
    [hotelId, proveedorId, desde, hasta, soloPendientes],
  );

  const { data, isLoading } = useQuery({
    queryKey: ['cuentas-por-pagar', filtros],
    queryFn: () => listarCuentasPorPagar(filtros),
  });
  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: proveedores } = useQuery({ queryKey: ['proveedores'], queryFn: () => listarProveedores(true) });

  const pagoMutation = useMutation({
    mutationFn: (payload: RegistrarPagoProveedorRequest) => registrarPagoProveedor(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cuentas-por-pagar'] });
      qc.invalidateQueries({ queryKey: ['documentos'] });
      cerrarPago();
    },
    onError: (e: unknown) => {
      const msg =
        (e as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'No se pudo registrar el pago.';
      setError(msg);
    },
  });

  const eliminarPagoMutation = useMutation({
    mutationFn: eliminarPagoProveedor,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cuentas-por-pagar'] });
      qc.invalidateQueries({ queryKey: ['documentos'] });
      setErrorTabla(null);
    },
    onError: (e: unknown) => {
      const msg =
        (e as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        'No se pudo eliminar el pago.';
      setErrorTabla(msg);
    },
  });

  const abrirPago = (cuenta: CuentaPorPagar) => {
    setCuentaSeleccionada(cuenta);
    setMonto(String(cuenta.saldo));
    setFechaPago(fechaInput(new Date()));
    setMetodoPago('Transferencia');
    setReferencia('');
    setObservaciones('');
    setError(null);
  };

  const cerrarPago = () => {
    setCuentaSeleccionada(null);
    setMonto('');
    setError(null);
  };

  const registrarPago = (e: React.FormEvent) => {
    e.preventDefault();
    if (!cuentaSeleccionada) return;
    setError(null);
    pagoMutation.mutate({
      documentoCompraId: cuentaSeleccionada.documentoCompraId,
      fecha: fechaPago,
      monto: Number(monto),
      metodoPago,
      referencia: referencia || undefined,
      observaciones: observaciones || undefined,
    });
  };

  const alternarPagos = (documentoCompraId: number) => {
    setCuentasAbiertas((prev) => ({ ...prev, [documentoCompraId]: !prev[documentoCompraId] }));
  };

  const eliminarPago = (pago: PagoProveedor) => {
    if (!window.confirm(`Eliminar pago de ${Q(pago.monto)}?`)) return;
    eliminarPagoMutation.mutate(pago.id);
  };

  const exportarExcel = async () => {
    setDescargandoExcel(true);
    setErrorTabla(null);
    try {
      await descargarCuentasPorPagarExcel(filtros);
    } catch {
      setErrorTabla('No se pudo exportar cuentas por pagar.');
    } finally {
      setDescargandoExcel(false);
    }
  };

  const usarMesActual = () => {
    const hoy = new Date();
    setDesde(fechaInput(new Date(hoy.getFullYear(), hoy.getMonth(), 1)));
    setHasta(fechaInput(new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0)));
  };

  const resumen = data?.resumen;
  const cuentas = data?.cuentas ?? [];

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(14,165,233,0.28),rgba(15,23,42,0)_58%)]" />
          <div className="relative">
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-cyan-300/80">Finanzas</p>
            <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">Cuentas por pagar</h1>
            <p className="mt-1 max-w-2xl text-sm text-slate-300">
              Controla saldos por factura, vencimientos de credito y abonos registrados a proveedores.
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi titulo="Saldo pendiente" valor={Q(resumen?.saldoPendiente ?? 0)} detalle={`${resumen?.documentosPendientes ?? 0} documentos abiertos`} />
        <Kpi titulo="Saldo vencido" valor={Q(resumen?.saldoVencido ?? 0)} detalle={`${resumen?.documentosVencidos ?? 0} documentos vencidos`} />
        <Kpi titulo="Pagado" valor={Q(resumen?.pagado ?? 0)} detalle="segun filtros actuales" />
        <Kpi titulo="Neto periodo" valor={Q(resumen?.netoAPagar ?? 0)} detalle="facturas recibidas" />
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi titulo="Por vencer" valor={Q(resumen?.porVencer ?? 0)} detalle="saldo aun dentro del credito" />
        <Kpi titulo="Vencido 0-30" valor={Q(resumen?.vencido0A30 ?? 0)} detalle="primer tramo vencido" />
        <Kpi titulo="Vencido 31-60" valor={Q(resumen?.vencido31A60 ?? 0)} detalle="requiere seguimiento" />
        <Kpi titulo="Vencido 61+" valor={Q(resumen?.vencido61Mas ?? 0)} detalle="prioridad critica" />
      </div>

      <div className="card card-pad">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(180px,1fr)_minmax(220px,1.2fr)_150px_150px_auto_auto_auto] lg:items-end">
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
            <label className="label">Proveedor</label>
            <select value={proveedorId} onChange={(e) => setProveedorId(e.target.value === '' ? '' : Number(e.target.value))} className="field">
              <option value="">Todos</option>
              {proveedores?.map((p) => (
                <option key={p.id} value={p.id}>{p.nombre}</option>
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
          <label className="inline-flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-600">
            <input type="checkbox" checked={soloPendientes} onChange={(e) => setSoloPendientes(e.target.checked)} />
            Solo pendientes
          </label>
          <button type="button" onClick={usarMesActual} className="btn-secondary">Mes actual</button>
          <button type="button" onClick={() => void exportarExcel()} disabled={descargandoExcel} className="btn-primary">
            {descargandoExcel ? 'Exportando...' : 'Exportar Excel'}
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div className="card-header">
          <div>
            <h2 className="card-title">Facturas y saldos</h2>
            <p className="mt-1 text-xs text-slate-500">Los vencimientos usan los dias de credito configurados en cada proveedor.</p>
          </div>
          {isLoading && <span className="badge-slate">Cargando</span>}
        </div>
        {errorTabla && (
          <div className="border-b border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{errorTabla}</div>
        )}
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Vence</th>
                <th className="th">Documento</th>
                <th className="th">Proveedor</th>
                <th className="th">Hotel</th>
                <th className="th">Estado</th>
                <th className="th text-right">Neto</th>
                <th className="th text-right">Pagado</th>
                <th className="th text-right">Saldo</th>
                <th className="th text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={9} className="empty-cell">Cargando cuentas...</td>
                </tr>
              )}
              {!isLoading && cuentas.map((c) => (
                <Fragment key={c.documentoCompraId}>
                  <tr className="trow">
                    <td className="td whitespace-nowrap">
                      <div className="font-medium text-slate-800">{fechaCorta(c.fechaVencimiento)}</div>
                      <div className="text-xs text-slate-400">{c.diasCredito} dias credito</div>
                    </td>
                    <td className="td">
                      <div className="font-semibold text-slate-800">{c.numeroDocumento}</div>
                      <div className="text-xs text-slate-400">{fechaCorta(c.fecha)}</div>
                    </td>
                    <td className="td text-slate-600">{c.proveedorNombre}</td>
                    <td className="td text-slate-600">{c.hotelNombre}</td>
                    <td className="td"><span className={badgeEstado(c.estado)}>{c.estado}</span></td>
                    <td className="td text-right font-medium text-slate-800">{Q(c.netoAPagar)}</td>
                    <td className="td text-right text-slate-600">
                      <div>{Q(c.pagado)}</div>
                      {c.pagos.length > 0 && (
                        <button
                          type="button"
                          onClick={() => alternarPagos(c.documentoCompraId)}
                          className="mt-1 text-xs font-medium text-cyan-700 hover:text-cyan-900"
                        >
                          {cuentasAbiertas[c.documentoCompraId] ? 'Ocultar pagos' : `Ver ${c.pagos.length} pagos`}
                        </button>
                      )}
                    </td>
                    <td className="td text-right font-semibold text-slate-900">{Q(c.saldo)}</td>
                    <td className="td text-right">
                      <div className="flex justify-end gap-2">
                        {c.saldo > 0 ? (
                          <button type="button" onClick={() => abrirPago(c)} className="btn-primary btn-sm">
                            Registrar pago
                          </button>
                        ) : (
                          <span className="text-xs text-slate-400">Liquidado</span>
                        )}
                      </div>
                    </td>
                  </tr>
                  {cuentasAbiertas[c.documentoCompraId] && (
                    <tr className="bg-slate-50/70">
                      <td colSpan={9} className="px-4 py-3">
                        <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
                          <table className="w-full text-xs">
                            <thead className="bg-slate-50 text-left uppercase tracking-wide text-slate-400">
                              <tr>
                                <th className="px-3 py-2">Fecha</th>
                                <th className="px-3 py-2">Metodo</th>
                                <th className="px-3 py-2">Referencia</th>
                                <th className="px-3 py-2">Observaciones</th>
                                <th className="px-3 py-2">Registro</th>
                                <th className="px-3 py-2 text-right">Monto</th>
                                <th className="px-3 py-2"></th>
                              </tr>
                            </thead>
                            <tbody>
                              {c.pagos.map((p) => (
                                <tr key={p.id} className="border-t border-slate-100">
                                  <td className="px-3 py-2 text-slate-600">{fechaCorta(p.fecha)}</td>
                                  <td className="px-3 py-2 text-slate-600">{p.metodoPago}</td>
                                  <td className="px-3 py-2 text-slate-500">{p.referencia ?? '-'}</td>
                                  <td className="px-3 py-2 text-slate-500">{p.observaciones ?? '-'}</td>
                                  <td className="px-3 py-2 text-slate-500">
                                    <div>{p.creadoPor ?? '-'}</div>
                                    <div className="text-[11px] text-slate-400">{new Date(p.creadoEn).toLocaleString('es-GT')}</div>
                                  </td>
                                  <td className="px-3 py-2 text-right font-semibold text-slate-800">{Q(p.monto)}</td>
                                  <td className="px-3 py-2 text-right">
                                    <button
                                      type="button"
                                      onClick={() => eliminarPago(p)}
                                      disabled={eliminarPagoMutation.isPending}
                                      className="rounded-md px-2 py-1 font-medium text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
                                    >
                                      Eliminar
                                    </button>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {!isLoading && cuentas.length === 0 && (
                <tr>
                  <td colSpan={9} className="empty-cell">No hay cuentas por pagar con estos filtros.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {cuentaSeleccionada && (
        <div className="fixed inset-0 z-50">
          <div className="absolute inset-0 bg-slate-950/50 backdrop-blur-sm" onClick={cerrarPago} />
          <form onSubmit={registrarPago} className="absolute right-0 top-0 flex h-full w-full max-w-md flex-col bg-white shadow-2xl">
            <div className="border-b border-slate-200 px-5 py-4">
              <p className="text-xs font-medium uppercase tracking-wide text-cyan-600">Pago proveedor</p>
              <h2 className="mt-1 text-lg font-semibold text-slate-900">{cuentaSeleccionada.numeroDocumento}</h2>
              <p className="mt-1 text-sm text-slate-500">{cuentaSeleccionada.proveedorNombre} · saldo {Q(cuentaSeleccionada.saldo)}</p>
            </div>
            <div className="flex-1 space-y-4 overflow-y-auto px-5 py-5">
              {error && <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">{error}</div>}
              <div>
                <label className="label">Fecha *</label>
                <input type="date" value={fechaPago} onChange={(e) => setFechaPago(e.target.value)} required className="field" />
              </div>
              <div>
                <label className="label">Monto *</label>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  max={cuentaSeleccionada.saldo}
                  value={monto}
                  onChange={(e) => setMonto(e.target.value)}
                  required
                  className="field"
                />
              </div>
              <div>
                <label className="label">Metodo</label>
                <select value={metodoPago} onChange={(e) => setMetodoPago(e.target.value)} className="field">
                  <option>Transferencia</option>
                  <option>Cheque</option>
                  <option>Efectivo</option>
                  <option>Tarjeta</option>
                  <option>Otro</option>
                </select>
              </div>
              <div>
                <label className="label">Referencia</label>
                <input value={referencia} onChange={(e) => setReferencia(e.target.value)} className="field" />
              </div>
              <div>
                <label className="label">Observaciones</label>
                <textarea value={observaciones} onChange={(e) => setObservaciones(e.target.value)} className="field min-h-24" />
              </div>
            </div>
            <div className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-5 py-4">
              <button type="button" onClick={cerrarPago} className="btn-secondary">Cancelar</button>
              <button type="submit" disabled={pagoMutation.isPending} className="btn-primary">
                {pagoMutation.isPending ? 'Guardando...' : 'Registrar pago'}
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
