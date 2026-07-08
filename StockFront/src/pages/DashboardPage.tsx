import { useMemo, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  alertasPrecio,
  consumoHoteles,
  obtenerGerencial,
  obtenerResumen,
  tendenciaPrecio,
  topCaros,
  topComprados,
} from '../features/dashboard/dashboardApi';
import { listarProductos } from '../features/catalogos/catalogosApi';
import type { GastoPorCategoria, GastoPorHotel, TopProducto } from '../features/dashboard/types';
import { listarAlertasStock, type AlertaStock } from '../features/inventario/inventarioApi';

const COLORES = ['#059669', '#0ea5e9', '#7c3aed', '#d97706', '#e11d48', '#0d9488'];
const MESES_CORTOS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
const MESES_LARGOS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

const etiquetaMes = (anio: number, mes: number) => `${MESES_CORTOS[mes - 1]} ${String(anio).slice(2)}`;
const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const numero = (n: number) => n.toLocaleString('es-GT', { maximumFractionDigits: 2 });

function IconoTendencia({ tipo }: { tipo: 'up' | 'down' | 'flat' }) {
  const path =
    tipo === 'up'
      ? 'M3 17 9 11l4 4 8-8M14 7h7v7'
      : tipo === 'down'
        ? 'M3 7l6 6 4-4 8 8M14 17h7v-7'
        : 'M4 12h16';
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-5 w-5">
      <path strokeLinecap="round" strokeLinejoin="round" d={path} />
    </svg>
  );
}

function TarjetaKpi({
  titulo,
  valor,
  detalle,
  tono,
  icono,
}: {
  titulo: string;
  valor: string;
  detalle?: string;
  tono?: 'ok' | 'mal' | 'neutro';
  icono?: ReactNode;
}) {
  const estilos =
    tono === 'mal'
      ? 'bg-rose-50 text-rose-700 ring-rose-200'
      : tono === 'ok'
        ? 'bg-emerald-50 text-emerald-700 ring-emerald-200'
        : 'bg-slate-100 text-slate-600 ring-slate-200';

  return (
    <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200/70">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs font-medium uppercase tracking-wide text-slate-400">{titulo}</div>
          <div
            className={`mt-2 text-2xl font-semibold tracking-tight ${
              tono === 'mal' ? 'text-rose-600' : tono === 'ok' ? 'text-emerald-600' : 'text-slate-900'
            }`}
          >
            {valor}
          </div>
        </div>
        {icono && <div className={`grid h-9 w-9 place-items-center rounded-lg ring-1 ${estilos}`}>{icono}</div>}
      </div>
      {detalle && <div className="mt-2 text-xs text-slate-500">{detalle}</div>}
    </div>
  );
}

function badgeEstadoStock(estado: AlertaStock['estadoStock']) {
  if (estado === 'Negativo') return 'badge-red';
  if (estado === 'SinStock' || estado === 'BajoMinimo') return 'badge-amber';
  return 'badge-slate';
}

function textoEstadoStock(estado: AlertaStock['estadoStock']) {
  const labels: Record<AlertaStock['estadoStock'], string> = {
    Ok: 'OK',
    BajoMinimo: 'Bajo mínimo',
    SinStock: 'Sin stock',
    Negativo: 'Negativo',
    SinConfigurar: 'Sin mínimo',
  };
  return labels[estado];
}

function TablaTop({ titulo, filas, columnaValor }: { titulo: string; filas?: TopProducto[]; columnaValor: 'gasto' | 'precio' }) {
  return (
    <div className="card overflow-hidden">
      <div className="card-header">
        <h3 className="card-title">{titulo}</h3>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="thead">
            <tr>
              <th className="th">#</th>
              <th className="th">Producto</th>
              <th className="th text-right">Cantidad</th>
              <th className="th text-right">{columnaValor === 'gasto' ? 'Gasto' : 'Precio prom.'}</th>
            </tr>
          </thead>
          <tbody>
            {filas?.map((p, i) => (
              <tr key={p.productoId} className="trow">
                <td className="td text-slate-400">{i + 1}</td>
                <td className="td">
                  <div className="font-medium text-slate-800">{p.producto}</div>
                  <div className="text-xs text-slate-400">{p.categoria}</div>
                </td>
                <td className="td text-right text-slate-600">
                  {p.cantidadBase.toLocaleString('es-GT')} {p.unidadBase}
                </td>
                <td className="td text-right font-semibold text-slate-800">
                  {columnaValor === 'gasto' ? Q(p.gastoTotal) : `${Q(p.precioPromedioBase)}/${p.unidadBase}`}
                </td>
              </tr>
            ))}
            {(!filas || filas.length === 0) && (
              <tr>
                <td colSpan={4} className="empty-cell">Sin datos todavía.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function BarrasCategoria({ categorias }: { categorias: GastoPorCategoria[] }) {
  const total = categorias.reduce((acc, c) => acc + c.gasto, 0);
  const ordenadas = [...categorias].sort((a, b) => b.gasto - a.gasto);

  return (
    <div className="card card-pad">
      <div className="mb-4 flex items-center justify-between gap-3">
        <div>
          <h3 className="card-title">Gasto por categoría</h3>
          <p className="mt-1 text-xs text-slate-500">Peso relativo dentro del mes seleccionado.</p>
        </div>
        <span className="badge-slate">{Q(total)}</span>
      </div>
      <div className="space-y-3">
        {ordenadas.map((c, i) => {
          const porcentaje = total > 0 ? (c.gasto / total) * 100 : 0;
          return (
            <div key={c.categoria}>
              <div className="mb-1 flex items-center justify-between gap-3 text-sm">
                <span className="font-medium text-slate-700">{c.categoria}</span>
                <span className="text-slate-500">{Q(c.gasto)}</span>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                <div className="h-full rounded-full" style={{ width: `${porcentaje}%`, backgroundColor: COLORES[i % COLORES.length] }} />
              </div>
            </div>
          );
        })}
        {ordenadas.length === 0 && <div className="py-8 text-center text-sm text-slate-400">Sin compras categorizadas este mes.</div>}
      </div>
    </div>
  );
}

function PresupuestoHoteles({ hoteles }: { hoteles: GastoPorHotel[] }) {
  return (
    <div className="card overflow-hidden">
      <div className="card-header">
        <div>
          <h3 className="card-title">Food cost y presupuesto</h3>
          <p className="mt-1 text-xs text-slate-500">Comparativo mensual por hotel.</p>
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="thead">
            <tr>
              <th className="th">Hotel</th>
              <th className="th text-right">Gasto</th>
              <th className="th text-right">Comensales</th>
              <th className="th text-right">Costo/comensal</th>
              <th className="th text-right">Presupuesto</th>
              <th className="th text-right">% usado</th>
            </tr>
          </thead>
          <tbody>
            {hoteles.map((h) => (
              <tr key={h.hotelId} className="trow">
                <td className="td font-medium text-slate-700">{h.hotel}</td>
                <td className="td text-right text-slate-600">{Q(h.gasto)}</td>
                <td className="td text-right text-slate-600">{h.comensales?.toLocaleString('es-GT') ?? '—'}</td>
                <td className="td text-right font-semibold text-slate-800">
                  {h.costoPorComensal != null ? Q(h.costoPorComensal) : '—'}
                </td>
                <td className="td text-right text-slate-600">{h.presupuesto != null ? Q(h.presupuesto) : '—'}</td>
                <td className="td min-w-40 text-right">
                  {h.porcentajePresupuesto != null ? (
                    <div className="flex items-center justify-end gap-2">
                      <div className="h-2 w-20 overflow-hidden rounded-full bg-slate-100">
                        <div
                          className={`h-full rounded-full ${
                            h.porcentajePresupuesto > 100
                              ? 'bg-rose-500'
                              : h.porcentajePresupuesto > 85
                                ? 'bg-amber-500'
                                : 'bg-emerald-500'
                          }`}
                          style={{ width: `${Math.min(h.porcentajePresupuesto, 120)}%` }}
                        />
                      </div>
                      <span
                        className={
                          h.porcentajePresupuesto > 100
                            ? 'badge-red'
                            : h.porcentajePresupuesto > 85
                              ? 'badge-amber'
                              : 'badge-green'
                        }
                      >
                        {h.porcentajePresupuesto}%
                      </span>
                    </div>
                  ) : (
                    '—'
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function DashboardPage() {
  const hoy = new Date();
  const [anio, setAnio] = useState(hoy.getFullYear());
  const [mes, setMes] = useState(hoy.getMonth() + 1);
  const [ventanaMeses, setVentanaMeses] = useState(6);
  const [umbralAlertas, setUmbralAlertas] = useState(15);

  const { data: resumen } = useQuery({
    queryKey: ['dash-resumen', anio, mes],
    queryFn: () => obtenerResumen(anio, mes),
  });
  const { data: gerencial } = useQuery({
    queryKey: ['dash-gerencial', anio, mes],
    queryFn: () => obtenerGerencial(anio, mes),
  });
  const { data: comprados } = useQuery({
    queryKey: ['dash-top-comprados', ventanaMeses],
    queryFn: () => topComprados(ventanaMeses),
  });
  const { data: caros } = useQuery({
    queryKey: ['dash-top-caros', ventanaMeses],
    queryFn: () => topCaros(ventanaMeses),
  });
  const { data: consumo } = useQuery({
    queryKey: ['dash-consumo', ventanaMeses],
    queryFn: () => consumoHoteles(ventanaMeses),
  });
  const { data: alertas } = useQuery({
    queryKey: ['dash-alertas', umbralAlertas],
    queryFn: () => alertasPrecio(umbralAlertas),
  });
  const { data: alertasStock } = useQuery({
    queryKey: ['inventario-alertas-stock'],
    queryFn: listarAlertasStock,
  });
  const { data: productos } = useQuery({ queryKey: ['productos'], queryFn: () => listarProductos(true) });

  const [productoTendencia, setProductoTendencia] = useState<number | ''>('');
  const { data: tendencia } = useQuery({
    queryKey: ['dash-tendencia', productoTendencia],
    queryFn: () => tendenciaPrecio(Number(productoTendencia)),
    enabled: productoTendencia !== '',
  });

  const datosConsumo = useMemo(() => {
    if (!consumo) return [];
    const claves = new Map<string, { anio: number; mes: number }>();
    for (const h of consumo) {
      for (const p of h.serie) claves.set(`${p.anio}-${p.mes}`, { anio: p.anio, mes: p.mes });
    }

    return [...claves.values()]
      .sort((a, b) => a.anio - b.anio || a.mes - b.mes)
      .map(({ anio: anioSerie, mes: mesSerie }) => {
        const fila: Record<string, number | string> = { label: etiquetaMes(anioSerie, mesSerie) };
        for (const h of consumo) {
          const punto = h.serie.find((p) => p.anio === anioSerie && p.mes === mesSerie);
          if (punto) fila[h.hotel] = punto.valor;
        }
        return fila;
      });
  }, [consumo]);

  const datosTendencia = useMemo(
    () => tendencia?.serie.map((p) => ({ label: etiquetaMes(p.anio, p.mes), precio: p.valor })) ?? [],
    [tendencia],
  );

  const hotelMayorGasto = resumen?.porHotel[0];
  const requiereGestion = resumen ? !resumen.porHotel.some((h) => h.comensales != null || h.presupuesto != null) : false;
  const variacionTipo = !resumen || resumen.variacionPorcentaje === 0 ? 'flat' : resumen.variacionPorcentaje > 0 ? 'up' : 'down';

  return (
    <div className="space-y-6">
      <div className="overflow-hidden rounded-2xl bg-slate-950 shadow-sm">
        <div className="relative px-5 py-6 sm:px-6 lg:px-8">
          <div className="absolute inset-0 bg-[radial-gradient(75%_75%_at_80%_0%,rgba(14,165,233,0.22),rgba(15,23,42,0)_58%)]" />
          <div className="relative flex flex-wrap items-end justify-between gap-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-emerald-300/80">Panel ejecutivo</p>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight text-white">
                {MESES_LARGOS[mes - 1]} {anio}
              </h1>
              <p className="mt-1 max-w-2xl text-sm text-slate-300">
                Compras, precios, presupuesto y consumo de los hoteles en una sola lectura.
              </p>
            </div>

            <div className="grid grid-cols-2 gap-2 sm:flex sm:items-center">
              <select value={mes} onChange={(e) => setMes(Number(e.target.value))} className="field bg-white">
                {MESES_LARGOS.map((m, i) => (
                  <option key={m} value={i + 1}>
                    {m}
                  </option>
                ))}
              </select>
              <input type="number" value={anio} onChange={(e) => setAnio(Number(e.target.value))} className="field w-28 bg-white" />
              <select value={ventanaMeses} onChange={(e) => setVentanaMeses(Number(e.target.value))} className="field bg-white sm:w-36">
                <option value={3}>3 meses</option>
                <option value={6}>6 meses</option>
                <option value={12}>12 meses</option>
              </select>
              <select value={umbralAlertas} onChange={(e) => setUmbralAlertas(Number(e.target.value))} className="field bg-white sm:w-40">
                <option value={10}>Alerta +10%</option>
                <option value={15}>Alerta +15%</option>
                <option value={20}>Alerta +20%</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <TarjetaKpi
          titulo="Gasto del mes"
          valor={resumen ? Q(resumen.gastoTotal) : '—'}
          detalle={resumen ? `mes anterior: ${Q(resumen.gastoMesAnterior)}` : 'Cargando resumen'}
          icono={<IconoTendencia tipo="flat" />}
        />
        <TarjetaKpi
          titulo="Variación"
          valor={resumen ? `${resumen.variacionPorcentaje > 0 ? '+' : ''}${resumen.variacionPorcentaje}%` : '—'}
          tono={resumen ? (resumen.variacionPorcentaje > 0 ? 'mal' : resumen.variacionPorcentaje < 0 ? 'ok' : 'neutro') : 'neutro'}
          detalle="contra mes anterior"
          icono={<IconoTendencia tipo={variacionTipo} />}
        />
        <TarjetaKpi
          titulo="Documentos"
          valor={resumen ? String(resumen.documentosRegistrados) : '—'}
          detalle="registrados en el mes"
          icono={<IconoTendencia tipo="flat" />}
        />
        <TarjetaKpi
          titulo="Hotel mayor gasto"
          valor={hotelMayorGasto ? Q(hotelMayorGasto.gasto) : '—'}
          detalle={hotelMayorGasto?.hotel ?? 'Sin compras en el periodo'}
          icono={<IconoTendencia tipo="flat" />}
        />
        <TarjetaKpi
          titulo="Alertas stock"
          valor={alertasStock ? String(alertasStock.length) : '—'}
          tono={alertasStock && alertasStock.length > 0 ? 'mal' : 'ok'}
          detalle="mínimos incumplidos"
          icono={<IconoTendencia tipo={alertasStock && alertasStock.length > 0 ? 'down' : 'flat'} />}
        />
      </div>

      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
          <TarjetaKpi
            titulo="Inventario estimado"
            valor={gerencial ? Q(gerencial.valorInventarioEstimado) : '—'}
            detalle="existencia positiva x ultimo precio"
            tono="ok"
            icono={<IconoTendencia tipo="flat" />}
          />
          <TarjetaKpi
            titulo="Faltante critico"
            valor={gerencial ? Q(gerencial.valorFaltanteEstimado) : '—'}
            detalle={gerencial ? `${gerencial.productosEnRiesgo} productos bajo minimo` : 'Cargando'}
            tono={gerencial && gerencial.productosEnRiesgo > 0 ? 'mal' : 'ok'}
            icono={<IconoTendencia tipo={gerencial && gerencial.productosEnRiesgo > 0 ? 'down' : 'flat'} />}
          />
          <TarjetaKpi
            titulo="Mermas del mes"
            valor={gerencial ? Q(gerencial.valorMermasEstimado) : '—'}
            detalle={gerencial ? `${gerencial.movimientosMerma} movimientos` : 'Cargando'}
            tono={gerencial && gerencial.valorMermasEstimado > 0 ? 'mal' : 'ok'}
            icono={<IconoTendencia tipo={gerencial && gerencial.valorMermasEstimado > 0 ? 'down' : 'flat'} />}
          />
          <TarjetaKpi
            titulo="Ajustes valorizados"
            valor={gerencial ? Q(gerencial.valorAjustesEstimado) : '—'}
            detalle={gerencial ? `${gerencial.movimientosAjuste} ajustes del mes` : 'Cargando'}
            icono={<IconoTendencia tipo="flat" />}
          />
          <TarjetaKpi
            titulo="Cuentas vencidas"
            valor={gerencial?.incluyeFinanzas ? Q(gerencial.saldoCuentasVencido ?? 0) : '—'}
            detalle={
              gerencial?.incluyeFinanzas
                ? `${gerencial.documentosVencidos ?? 0} documentos vencidos`
                : 'visible para Admin/Gerencia'
            }
            tono={gerencial?.incluyeFinanzas && (gerencial.saldoCuentasVencido ?? 0) > 0 ? 'mal' : 'ok'}
            icono={<IconoTendencia tipo={gerencial?.incluyeFinanzas && (gerencial.saldoCuentasVencido ?? 0) > 0 ? 'down' : 'flat'} />}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
          <div className="card overflow-hidden">
            <div className="card-header">
              <div>
                <h3 className="card-title">Proveedores por saldo</h3>
                <p className="mt-1 text-xs text-slate-500">Cuentas abiertas al cierre del mes.</p>
              </div>
              {gerencial?.incluyeFinanzas && <span className="badge-slate">{Q(gerencial.saldoCuentasPorPagar ?? 0)}</span>}
            </div>
            <div className="divide-y divide-slate-100">
              {gerencial?.incluyeFinanzas && gerencial.topProveedoresSaldo.map((p) => (
                <div key={p.proveedorId} className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 px-4 py-3 text-sm">
                  <div>
                    <div className="font-semibold text-slate-800">{p.proveedor}</div>
                    <div className="text-xs text-slate-400">{p.documentosPendientes} documentos pendientes</div>
                  </div>
                  <div className="text-right">
                    <div className="font-semibold text-slate-900">{Q(p.saldo)}</div>
                    {p.saldoVencido > 0 && <div className="text-xs font-medium text-rose-600">vencido {Q(p.saldoVencido)}</div>}
                  </div>
                </div>
              ))}
              {gerencial?.incluyeFinanzas && gerencial.topProveedoresSaldo.length === 0 && (
                <div className="empty-cell">Sin saldos pendientes.</div>
              )}
              {gerencial && !gerencial.incluyeFinanzas && (
                <div className="empty-cell">Disponible para Admin/Gerencia.</div>
              )}
            </div>
          </div>

          <div className="card overflow-hidden">
            <div className="card-header">
              <div>
                <h3 className="card-title">Top mermas</h3>
                <p className="mt-1 text-xs text-slate-500">Valorizadas con ultimo precio conocido.</p>
              </div>
            </div>
            <div className="divide-y divide-slate-100">
              {gerencial?.topMermas.map((m) => (
                <div key={m.productoId} className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 px-4 py-3 text-sm">
                  <div>
                    <div className="font-semibold text-slate-800">{m.producto}</div>
                    <div className="text-xs text-slate-400">
                      {numero(m.cantidadBase)} {m.unidadBase} - {m.categoria}
                    </div>
                  </div>
                  <div className="font-semibold text-rose-700">{Q(m.valorEstimado)}</div>
                </div>
              ))}
              {gerencial?.topMermas.length === 0 && <div className="empty-cell">Sin mermas en el mes.</div>}
            </div>
          </div>

          <div className="card overflow-hidden">
            <div className="card-header">
              <div>
                <h3 className="card-title">Stock critico</h3>
                <p className="mt-1 text-xs text-slate-500">Productos con minimo incumplido.</p>
              </div>
            </div>
            <div className="divide-y divide-slate-100">
              {gerencial?.stockCritico.slice(0, 6).map((s) => (
                <div key={`${s.hotelId}-${s.productoId}`} className="px-4 py-3 text-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-semibold text-slate-800">{s.producto}</div>
                      <div className="text-xs text-slate-400">{s.hotel}</div>
                    </div>
                    <span className={badgeEstadoStock(s.estadoStock)}>{textoEstadoStock(s.estadoStock)}</span>
                  </div>
                  <div className="mt-2 grid grid-cols-3 gap-2 text-xs text-slate-500">
                    <div>Exist. <span className="font-semibold text-slate-700">{numero(s.existencia)}</span></div>
                    <div>Falta <span className="font-semibold text-amber-700">{numero(s.faltante)}</span></div>
                    <div className="text-right font-semibold text-slate-800">{Q(s.valorFaltanteEstimado)}</div>
                  </div>
                </div>
              ))}
              {gerencial?.stockCritico.length === 0 && <div className="empty-cell">Sin stock critico configurado.</div>}
            </div>
          </div>
        </div>
      </div>

      {alertas && alertas.length > 0 && (
        <div className="rounded-xl border border-rose-200 bg-white shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-rose-100 px-5 py-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-rose-800">
              <span className="grid h-8 w-8 place-items-center rounded-lg bg-rose-50 text-rose-600 ring-1 ring-rose-200">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-5 w-5">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
                </svg>
              </span>
              Productos con incremento de precio
            </div>
            <span className="badge-red">{alertas.length} alertas</span>
          </div>
          <div className="divide-y divide-rose-100">
            {alertas.slice(0, 6).map((a) => (
              <div key={a.productoId} className="grid gap-2 px-5 py-3 text-sm sm:grid-cols-[minmax(180px,1fr)_auto_auto] sm:items-center">
                <div>
                  <div className="font-semibold text-slate-800">{a.producto}</div>
                  <div className="text-xs text-slate-500">última compra: {a.ultimaCompra}</div>
                </div>
                <div className="text-slate-600">
                  {Q(a.precioReciente)}/{a.unidadBase} vs {Q(a.precioReferencia)}
                </div>
                <span className="badge bg-rose-600 text-white ring-rose-700/20">+{a.incrementoPorcentaje}%</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {alertasStock && alertasStock.length > 0 && (
        <div className="rounded-xl border border-amber-200 bg-white shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-amber-100 px-5 py-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-amber-900">
              <span className="grid h-8 w-8 place-items-center rounded-lg bg-amber-50 text-amber-700 ring-1 ring-amber-200">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-5 w-5">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M20.25 7.5 19.6 18.1a2.25 2.25 0 0 1-2.24 2.15H6.64A2.25 2.25 0 0 1 4.4 18.1L3.75 7.5M9 11.25h6M9 15h4.5M3.75 7.5h16.5l-1.5-3h-13.5l-1.5 3Z" />
                </svg>
              </span>
              Stock por debajo del mínimo
            </div>
            <span className="badge-amber">{alertasStock.length} alertas</span>
          </div>
          <div className="divide-y divide-amber-100">
            {alertasStock.slice(0, 8).map((a) => (
              <div key={`${a.hotelId}-${a.productoId}`} className="grid gap-2 px-5 py-3 text-sm lg:grid-cols-[minmax(180px,1fr)_minmax(160px,0.8fr)_auto_auto_auto] lg:items-center">
                <div>
                  <div className="font-semibold text-slate-800">{a.producto}</div>
                  <div className="text-xs text-slate-500">{a.categoria}</div>
                </div>
                <div className="text-slate-600">{a.hotel}</div>
                <div className="text-slate-600">
                  Existencia: <span className="font-medium text-slate-800">{numero(a.existencia)} {a.unidadBase}</span>
                </div>
                <div className="text-slate-600">
                  Faltante: <span className="font-semibold text-amber-800">{numero(a.faltante)} {a.unidadBase}</span>
                </div>
                <span className={badgeEstadoStock(a.estadoStock)}>{textoEstadoStock(a.estadoStock)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1.35fr_0.9fr]">
        <div className="card card-pad">
          <h3 className="card-title mb-4">Consumo mensual por hotel ({ventanaMeses} meses)</h3>
          <ResponsiveContainer width="100%" height={285}>
            <LineChart data={datosConsumo}>
              <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" />
              <XAxis dataKey="label" tick={{ fontSize: 12, fill: '#64748b' }} axisLine={{ stroke: '#e2e8f0' }} tickLine={false} />
              <YAxis tick={{ fontSize: 12, fill: '#64748b' }} axisLine={false} tickLine={false} />
              <Tooltip formatter={(v) => Q(Number(v))} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              {consumo?.map((h, i) => (
                <Line key={h.hotelId} type="monotone" dataKey={h.hotel} stroke={COLORES[i % COLORES.length]} strokeWidth={2} dot={{ r: 3 }} />
              ))}
            </LineChart>
          </ResponsiveContainer>
        </div>

        <BarrasCategoria categorias={resumen?.porCategoria ?? []} />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="card card-pad">
          <h3 className="card-title mb-4">Gasto del mes por hotel</h3>
          <ResponsiveContainer width="100%" height={260}>
            <BarChart data={resumen?.porHotel ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" vertical={false} />
              <XAxis dataKey="hotel" tick={{ fontSize: 11, fill: '#64748b' }} interval={0} axisLine={{ stroke: '#e2e8f0' }} tickLine={false} />
              <YAxis tick={{ fontSize: 12, fill: '#64748b' }} axisLine={false} tickLine={false} />
              <Tooltip formatter={(v) => Q(Number(v))} cursor={{ fill: '#f1f5f9' }} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
              <Bar dataKey="gasto" fill="#059669" radius={[6, 6, 0, 0]} maxBarSize={48} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="card card-pad">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <h3 className="card-title">Histórico de precio por producto</h3>
            <select
              value={productoTendencia}
              onChange={(e) => setProductoTendencia(e.target.value === '' ? '' : Number(e.target.value))}
              className="field w-auto min-w-52"
            >
              <option value="">Selecciona producto…</option>
              {productos?.map((p) => (
                <option key={p.id} value={p.id}>{p.nombre}</option>
              ))}
            </select>
          </div>
          {productoTendencia === '' ? (
            <div className="flex h-[240px] items-center justify-center rounded-xl bg-slate-50 text-sm text-slate-400">
              Elige un producto para ver su tendencia de precio.
            </div>
          ) : (
            <ResponsiveContainer width="100%" height={240}>
              <LineChart data={datosTendencia}>
                <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" />
                <XAxis dataKey="label" tick={{ fontSize: 12, fill: '#64748b' }} axisLine={{ stroke: '#e2e8f0' }} tickLine={false} />
                <YAxis tick={{ fontSize: 12, fill: '#64748b' }} domain={['auto', 'auto']} axisLine={false} tickLine={false} />
                <Tooltip formatter={(v) => `${Q(Number(v))}/${tendencia?.unidadBase ?? ''}`} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                <Line type="monotone" dataKey="precio" stroke="#0ea5e9" strokeWidth={2.5} dot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {resumen && resumen.porHotel.some((h) => h.comensales != null || h.presupuesto != null) && (
        <PresupuestoHoteles hoteles={resumen.porHotel} />
      )}

      {requiereGestion && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 px-5 py-4 text-sm text-amber-800">
          Agrega comensales y presupuestos en Gestión mensual para activar el análisis de food cost por hotel.
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <TablaTop titulo={`Top 10 más comprados (${ventanaMeses} meses)`} filas={comprados} columnaValor="gasto" />
        <TablaTop titulo={`Top 10 más caros (${ventanaMeses} meses)`} filas={caros} columnaValor="precio" />
      </div>
    </div>
  );
}
