import { useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { listarHoteles, listarProveedores } from '../features/catalogos/catalogosApi';
import { listarDocumentos } from '../features/compras/comprasApi';
import type { TipoCompra } from '../features/compras/types';
import {
  descargarExcel,
  descargarPdf,
  importarExcel,
  type ResultadoImportacion,
} from '../features/reportes/reportesApi';
import { useAuth } from '../features/auth/authStore';

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`;

const fechaInput = (fecha: Date) => {
  const local = new Date(fecha.getTime() - fecha.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
};

export function ReportesPage() {
  const qc = useQueryClient();
  const esAdmin = useAuth((s) => s.tieneRol('Admin'));
  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: proveedores } = useQuery({ queryKey: ['proveedores'], queryFn: () => listarProveedores(true) });

  const [hotelId, setHotelId] = useState<number | ''>('');
  const [proveedorId, setProveedorId] = useState<number | ''>('');
  const [tipoCompra, setTipoCompra] = useState<TipoCompra | ''>('');
  const [desde, setDesde] = useState('');
  const [hasta, setHasta] = useState('');
  const [descargando, setDescargando] = useState<'excel' | 'pdf' | null>(null);
  const [errorDescarga, setErrorDescarga] = useState<string | null>(null);

  const inputArchivo = useRef<HTMLInputElement>(null);

  interface ResultadoArchivo {
    archivo: string;
    resultado?: ResultadoImportacion;
    error?: string;
  }
  const [resultados, setResultados] = useState<ResultadoArchivo[]>([]);
  const [importando, setImportando] = useState(false);

  const filtro = useMemo(
    () => ({
      hotelId: hotelId === '' ? undefined : Number(hotelId),
      proveedorId: proveedorId === '' ? undefined : Number(proveedorId),
      tipoCompra: tipoCompra || undefined,
      desde: desde || undefined,
      hasta: hasta || undefined,
    }),
    [hotelId, proveedorId, tipoCompra, desde, hasta],
  );

  const { data: documentosPeriodo, isLoading: cargandoResumen } = useQuery({
    queryKey: ['documentos', 'reportes-resumen', filtro],
    queryFn: () => listarDocumentos(filtro),
  });

  const resumenPeriodo = useMemo(() => {
    const recibidos = (documentosPeriodo ?? []).filter((d) => d.estado === 'Recibido');
    const gasto = recibidos.reduce((acc, d) => acc + d.total, 0);
    const ordinaria = recibidos.filter((d) => (d.tipoCompra ?? 'Ordinaria') === 'Ordinaria').reduce((acc, d) => acc + d.total, 0);
    const extraordinaria = recibidos.filter((d) => d.tipoCompra === 'Extraordinaria').reduce((acc, d) => acc + d.total, 0);
    return {
      gasto,
      ordinaria,
      extraordinaria,
      documentos: recibidos.length,
      promedio: recibidos.length ? gasto / recibidos.length : 0,
      proveedores: new Set(recibidos.map((d) => d.proveedorId)).size,
      hoteles: new Set(recibidos.map((d) => d.hotelId)).size,
    };
  }, [documentosPeriodo]);

  const usarSemanaActual = () => {
    const hoy = new Date();
    const dia = hoy.getDay();
    const diasDesdeLunes = dia === 0 ? 6 : dia - 1;
    const inicio = new Date(hoy);
    inicio.setDate(hoy.getDate() - diasDesdeLunes);
    const fin = new Date(inicio);
    fin.setDate(inicio.getDate() + 6);
    setDesde(fechaInput(inicio));
    setHasta(fechaInput(fin));
  };

  const usarMesActual = () => {
    const hoy = new Date();
    const inicio = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
    const fin = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);
    setDesde(fechaInput(inicio));
    setHasta(fechaInput(fin));
  };

  const bajar = async (formato: 'excel' | 'pdf') => {
    setDescargando(formato);
    setErrorDescarga(null);
    try {
      await (formato === 'excel' ? descargarExcel(filtro) : descargarPdf(filtro));
    } catch {
      setErrorDescarga('No se pudo generar el reporte. Intenta de nuevo.');
    } finally {
      setDescargando(null);
    }
  };

  // Importa varios libros en secuencia (uno por mes); cada archivo reporta su resultado.
  const alSeleccionarArchivos = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const archivos = Array.from(e.target.files ?? []);
    e.target.value = '';
    if (archivos.length === 0) return;

    setResultados([]);
    setImportando(true);
    for (const archivo of archivos) {
      try {
        const resultado = await importarExcel(archivo);
        setResultados((prev) => [...prev, { archivo: archivo.name, resultado }]);
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
          'Error al importar (verifica el formato).';
        setResultados((prev) => [...prev, { archivo: archivo.name, error: msg }]);
      }
    }
    setImportando(false);
    // Los datos nuevos afectan a todo: documentos, productos y dashboard.
    qc.invalidateQueries();
  };

  const totales = resultados.reduce(
    (acc, r) => ({
      documentos: acc.documentos + (r.resultado?.documentosCreados ?? 0),
      omitidos: acc.omitidos + (r.resultado?.documentosOmitidos ?? 0),
      productos: acc.productos + (r.resultado?.productosCreados ?? 0),
      lineas: acc.lineas + (r.resultado?.lineasCreadas ?? 0),
    }),
    { documentos: 0, omitidos: 0, productos: 0, lineas: 0 },
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Reportes</h1>
        <p className="page-subtitle">Exporta reportes personalizados e importa los libros históricos de Excel.</p>
      </div>

      {/* Exportación */}
      <div className="card card-pad">
        <h2 className="card-title mb-1">Reporte de compras y liquidación</h2>
        <p className="mb-4 text-xs text-slate-500">
          Filtra por rango, hotel, proveedor o tipo de compra. El reporte usa solo documentos recibidos e incluye neto a pagar por proveedor.
        </p>
        {errorDescarga && <p className="mb-3 text-sm text-rose-600">{errorDescarga}</p>}
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="label">Hotel</label>
            <select
              value={hotelId}
              onChange={(e) => setHotelId(e.target.value === '' ? '' : Number(e.target.value))}
              className="field w-auto min-w-44"
            >
              <option value="">Todos los hoteles</option>
              {hoteles?.map((h) => (
                <option key={h.id} value={h.id}>
                  {h.nombre}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Proveedor</label>
            <select
              value={proveedorId}
              onChange={(e) => setProveedorId(e.target.value === '' ? '' : Number(e.target.value))}
              className="field w-auto min-w-56"
            >
              <option value="">Todos los proveedores</option>
              {proveedores?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.nombre}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Tipo compra</label>
            <select
              value={tipoCompra}
              onChange={(e) => setTipoCompra(e.target.value as TipoCompra | '')}
              className="field w-auto min-w-44"
            >
              <option value="">Todas</option>
              <option value="Ordinaria">Ordinaria</option>
              <option value="Extraordinaria">Extraordinaria</option>
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
          <button type="button" onClick={usarSemanaActual} className="btn-secondary">
            Semana actual
          </button>
          <button type="button" onClick={usarMesActual} className="btn-secondary">
            Mes actual
          </button>
          <button onClick={() => bajar('excel')} disabled={descargando !== null} className="btn-success">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-4 w-4">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" />
            </svg>
            {descargando === 'excel' ? 'Generando…' : 'Excel'}
          </button>
          <button onClick={() => bajar('pdf')} disabled={descargando !== null} className="btn-danger">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-4 w-4">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" />
            </svg>
            {descargando === 'pdf' ? 'Generando…' : 'PDF'}
          </button>
        </div>
        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-6">
          <div className="rounded-xl bg-slate-50 p-4 ring-1 ring-slate-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-slate-400">Total gastado</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? 'Calculando…' : Q(resumenPeriodo.gasto)}
            </div>
            <div className="mt-1 text-xs text-slate-500">Solo documentos recibidos.</div>
          </div>
          <div className="rounded-xl bg-sky-50 p-4 ring-1 ring-sky-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-sky-500">Ordinaria</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? 'Calculando…' : Q(resumenPeriodo.ordinaria)}
            </div>
            <div className="mt-1 text-xs text-sky-700">Compra planificada.</div>
          </div>
          <div className="rounded-xl bg-amber-50 p-4 ring-1 ring-amber-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-amber-600">Extraordinaria</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? 'Calculando…' : Q(resumenPeriodo.extraordinaria)}
            </div>
            <div className="mt-1 text-xs text-amber-700">Fuera del flujo normal.</div>
          </div>
          <div className="rounded-xl bg-slate-50 p-4 ring-1 ring-slate-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-slate-400">Documentos</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? '…' : resumenPeriodo.documentos}
            </div>
            <div className="mt-1 text-xs text-slate-500">Compras dentro del rango.</div>
          </div>
          <div className="rounded-xl bg-slate-50 p-4 ring-1 ring-slate-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-slate-400">Promedio</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? 'Calculando…' : Q(resumenPeriodo.promedio)}
            </div>
            <div className="mt-1 text-xs text-slate-500">Por documento recibido.</div>
          </div>
          <div className="rounded-xl bg-slate-50 p-4 ring-1 ring-slate-200/70">
            <div className="text-xs font-medium uppercase tracking-wide text-slate-400">Cobertura</div>
            <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900">
              {cargandoResumen ? '…' : `${resumenPeriodo.proveedores}/${resumenPeriodo.hoteles}`}
            </div>
            <div className="mt-1 text-xs text-slate-500">Proveedores / hoteles con compras.</div>
          </div>
        </div>
        <p className="mt-3 text-xs text-slate-400">
          El Excel incluye documentos, detalle por producto, resumen, liquidación de proveedores y facturas por proveedor.
          El PDF resume gasto, neto a pagar y proveedores del periodo.
        </p>
      </div>

      {/* Importación (solo Admin) */}
      {esAdmin && (
        <div className="card card-pad">
          <h2 className="card-title mb-1">Importar Excel histórico</h2>
          <p className="mb-4 text-xs text-slate-500">
            Puedes seleccionar <b>varios libros mensuales a la vez</b> (uno por mes, una hoja por hotel). Los documentos
            ya registrados se omiten automáticamente, así que es seguro repetir un archivo.
          </p>
          <input
            ref={inputArchivo}
            type="file"
            accept=".xlsx,.xlsm"
            multiple
            onChange={alSeleccionarArchivos}
            className="hidden"
          />
          <button onClick={() => inputArchivo.current?.click()} disabled={importando} className="btn-primary">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-4 w-4">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
            {importando ? `Importando… (${resultados.length} listo(s))` : 'Seleccionar archivos…'}
          </button>

          {resultados.length > 0 && (
            <div className="mt-4 space-y-2">
              {resultados.map((r) => (
                <div
                  key={r.archivo}
                  className={`rounded-lg border p-3 text-sm ${
                    r.error ? 'border-rose-200 bg-rose-50 text-rose-800' : 'border-emerald-200 bg-emerald-50 text-emerald-900'
                  }`}
                >
                  <div className="font-medium">{r.archivo}</div>
                  {r.error ? (
                    <div className="mt-0.5 text-xs">{r.error}</div>
                  ) : (
                    <div className="mt-0.5 text-xs">
                      {r.resultado!.documentosCreados} documentos · {r.resultado!.lineasCreadas} líneas ·{' '}
                      {r.resultado!.productosCreados} productos nuevos
                      {r.resultado!.documentosOmitidos > 0 && ` · ${r.resultado!.documentosOmitidos} omitidos (ya existían)`}
                      {r.resultado!.hojasNoReconocidas.length > 0 &&
                        ` · hojas no reconocidas: ${r.resultado!.hojasNoReconocidas.join(', ')}`}
                    </div>
                  )}
                </div>
              ))}

              {!importando && resultados.length > 1 && (
                <div className="rounded-lg bg-slate-100 p-3 text-sm font-medium text-slate-700 ring-1 ring-slate-200">
                  Total: {totales.documentos} documentos · {totales.lineas} líneas · {totales.productos} productos nuevos
                  {totales.omitidos > 0 && ` · ${totales.omitidos} omitidos`}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
