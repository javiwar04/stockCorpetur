import { Fragment, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPlato,
  crearPlato,
  eliminarIngrediente,
  impactoProducto,
  listarPlatos,
  upsertIngrediente,
  type Plato,
} from '../features/recetas/recetasApi';
import { listarProductos } from '../features/catalogos/catalogosApi';

const Q = (n: number) => `Q${n.toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

/** Semáforo de food cost: ≤30% sano, 30-40% atención, >40% problema. */
function BadgeFoodCost({ porcentaje }: { porcentaje: number | null }) {
  if (porcentaje == null) return <span className="text-slate-400">—</span>;
  const clase = porcentaje <= 30 ? 'badge-green' : porcentaje <= 40 ? 'badge-amber' : 'badge-red';
  return <span className={clase}>{porcentaje}%</span>;
}

function FilaIngredientes({ plato }: { plato: Plato }) {
  const qc = useQueryClient();
  const { data: productos } = useQuery({ queryKey: ['productos'], queryFn: () => listarProductos(true) });

  const [productoId, setProductoId] = useState<number | ''>('');
  const [cantidad, setCantidad] = useState('');
  const [error, setError] = useState<string | null>(null);

  const invalidar = () => qc.invalidateQueries({ queryKey: ['platos'] });

  const agregarMutation = useMutation({
    mutationFn: () =>
      upsertIngrediente(plato.id, { productoId: Number(productoId), cantidadPorPorcion: Number(cantidad) }),
    onSuccess: () => {
      invalidar();
      setProductoId('');
      setCantidad('');
      setError(null);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'No se pudo agregar el ingrediente.');
    },
  });

  const quitarMutation = useMutation({
    mutationFn: (ingredienteId: number) => eliminarIngrediente(plato.id, ingredienteId),
    onSuccess: invalidar,
  });

  return (
    <div className="space-y-3 rounded-xl bg-slate-50 p-4 text-sm ring-1 ring-slate-200/70">
      <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Receta (por porción)</div>

      {plato.ingredientes.length === 0 && (
        <p className="text-slate-400">Sin ingredientes aún — agrega el primero abajo.</p>
      )}

      {plato.ingredientes.length > 0 && (
        <table className="w-full">
          <thead className="text-left text-[11px] font-semibold uppercase tracking-wider text-slate-500">
            <tr>
              <th className="py-1 pr-2">Ingrediente</th>
              <th className="py-1 px-2 text-right">Cantidad</th>
              <th className="py-1 px-2 text-right">Precio actual</th>
              <th className="py-1 px-2 text-right">Costo</th>
              <th className="py-1 pl-2"></th>
            </tr>
          </thead>
          <tbody>
            {plato.ingredientes.map((i) => (
              <tr key={i.id} className="border-t border-slate-200">
                <td className="py-1.5 pr-2 text-slate-700">
                  {i.producto}
                  {!i.tienePrecio && <span className="ml-2 text-xs text-amber-600">(sin precio de compra aún)</span>}
                </td>
                <td className="py-1.5 px-2 text-right text-slate-600">
                  {i.cantidadPorPorcion} {i.unidadBase}
                </td>
                <td className="py-1.5 px-2 text-right text-slate-500">
                  {i.tienePrecio ? `${Q(i.precioUnitario)}/${i.unidadBase}` : '—'}
                </td>
                <td className="py-1.5 px-2 text-right font-medium text-slate-800">{Q(i.costoLinea)}</td>
                <td className="py-1.5 pl-2 text-right">
                  <button
                    onClick={() => quitarMutation.mutate(i.id)}
                    className="rounded-md px-1.5 py-0.5 text-xs text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
                  >
                    ✕
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {error && <p className="text-xs text-rose-600">{error}</p>}
      <div className="flex flex-wrap items-center gap-2">
        <select
          value={productoId}
          onChange={(e) => setProductoId(e.target.value === '' ? '' : Number(e.target.value))}
          className="field w-auto min-w-40"
        >
          <option value="">Agregar ingrediente…</option>
          {productos?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nombre}
            </option>
          ))}
        </select>
        <input
          type="number"
          step="0.001"
          min="0"
          placeholder="cantidad"
          value={cantidad}
          onChange={(e) => setCantidad(e.target.value)}
          className="field w-28"
        />
        <span className="text-xs text-slate-500">
          {productoId !== '' ? productos?.find((p) => p.id === productoId)?.unidadBaseNombre : 'unidad base'}
        </span>
        <button
          onClick={() => agregarMutation.mutate()}
          disabled={!productoId || !cantidad || Number(cantidad) <= 0 || agregarMutation.isPending}
          className="btn-primary btn-sm"
        >
          Agregar
        </button>
      </div>
    </div>
  );
}

export function PlatosPage() {
  const qc = useQueryClient();
  const { data: platos, isLoading } = useQuery({ queryKey: ['platos'], queryFn: () => listarPlatos(false) });
  const { data: productos } = useQuery({ queryKey: ['productos'], queryFn: () => listarProductos(true) });

  const [nombre, setNombre] = useState('');
  const [precioVenta, setPrecioVenta] = useState('');
  const [expandido, setExpandido] = useState<number | null>(null);

  const [productoImpacto, setProductoImpacto] = useState<number | ''>('');
  const { data: impacto } = useQuery({
    queryKey: ['impacto', productoImpacto],
    queryFn: () => impactoProducto(Number(productoImpacto)),
    enabled: productoImpacto !== '',
  });

  const crearMutation = useMutation({
    mutationFn: () => crearPlato({ nombre, precioVenta: precioVenta === '' ? undefined : Number(precioVenta) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['platos'] });
      setNombre('');
      setPrecioVenta('');
    },
  });

  const activoMutation = useMutation({
    mutationFn: (plato: Plato) =>
      actualizarPlato(plato.id, {
        nombre: plato.nombre,
        precioVenta: plato.precioVenta ?? undefined,
        activo: !plato.activo,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['platos'] }),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Menú y recetas</h1>
        <p className="page-subtitle">
          Costo por plato a precios recientes de compra. Food cost sano: ≤ 30% del precio de venta.
        </p>
      </div>

      {/* Nuevo plato */}
      <div className="card card-pad">
        <h2 className="card-title mb-4">Nuevo plato</h2>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            crearMutation.mutate();
          }}
          className="flex flex-wrap items-end gap-3"
        >
          <div className="min-w-52 flex-1">
            <label className="label">Nombre</label>
            <input value={nombre} onChange={(e) => setNombre(e.target.value)} required className="field" />
          </div>
          <div>
            <label className="label">Precio de venta (Q)</label>
            <input
              type="number"
              step="0.01"
              min="0"
              value={precioVenta}
              onChange={(e) => setPrecioVenta(e.target.value)}
              placeholder="opcional"
              className="field w-40"
            />
          </div>
          <button type="submit" disabled={crearMutation.isPending} className="btn-primary">
            Crear plato
          </button>
        </form>
      </div>

      {/* Platos */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Plato</th>
                <th className="th text-right">Costo</th>
                <th className="th text-right">Precio venta</th>
                <th className="th text-right">Margen</th>
                <th className="th text-center">Food cost</th>
                <th className="th">Estado</th>
                <th className="th"></th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={7} className="empty-cell">Cargando…</td>
                </tr>
              )}
              {platos?.map((p) => (
                <Fragment key={p.id}>
                  <tr className="trow">
                    <td className="td font-medium text-slate-700">
                      {p.nombre}
                      {!p.costoCompleto && (
                        <span
                          className="ml-2 text-xs text-amber-600"
                          title="Algún ingrediente no tiene precio de compra registrado"
                        >
                          ⚠ costo parcial
                        </span>
                      )}
                    </td>
                    <td className="td text-right text-slate-600">{Q(p.costo)}</td>
                    <td className="td text-right text-slate-600">{p.precioVenta != null ? Q(p.precioVenta) : '—'}</td>
                    <td className={`td text-right ${p.margen != null && p.margen < 0 ? 'font-medium text-rose-600' : 'text-slate-600'}`}>
                      {p.margen != null ? Q(p.margen) : '—'}
                    </td>
                    <td className="td text-center">
                      <BadgeFoodCost porcentaje={p.foodCostPorcentaje} />
                    </td>
                    <td className="td">
                      <span className={p.activo ? 'badge-green' : 'badge-slate'}>{p.activo ? 'Activo' : 'Inactivo'}</span>
                    </td>
                    <td className="td whitespace-nowrap text-right">
                      <button
                        onClick={() => setExpandido((prev) => (prev === p.id ? null : p.id))}
                        className="mr-1 rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                      >
                        {expandido === p.id ? 'Cerrar' : 'Receta'}
                      </button>
                      <button
                        onClick={() => activoMutation.mutate(p)}
                        className="rounded-md px-2 py-1 text-xs font-medium text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700"
                      >
                        {p.activo ? 'Desactivar' : 'Activar'}
                      </button>
                    </td>
                  </tr>
                  {expandido === p.id && (
                    <tr>
                      <td colSpan={7} className="px-5 pb-4">
                        <FilaIngredientes plato={p} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {platos?.length === 0 && (
                <tr>
                  <td colSpan={7} className="empty-cell">Sin platos registrados aún.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Impacto de un insumo */}
      <div className="card card-pad">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <h2 className="card-title">¿Qué platos se encarecen si sube un insumo?</h2>
          <select
            value={productoImpacto}
            onChange={(e) => setProductoImpacto(e.target.value === '' ? '' : Number(e.target.value))}
            className="field w-auto min-w-52"
          >
            <option value="">Selecciona producto…</option>
            {productos?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nombre}
              </option>
            ))}
          </select>
        </div>
        {productoImpacto === '' ? (
          <p className="text-sm text-slate-400">Elige un producto para ver en qué platos pesa su precio.</p>
        ) : impacto?.length === 0 ? (
          <p className="text-sm text-slate-400">Ningún plato activo usa este producto.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="thead">
                <tr>
                  <th className="th">Plato</th>
                  <th className="th text-right">Usa</th>
                  <th className="th text-right">Costo del insumo</th>
                  <th className="th text-right">Costo del plato</th>
                  <th className="th text-right">% del costo</th>
                </tr>
              </thead>
              <tbody>
                {impacto?.map((f) => (
                  <tr key={f.platoId} className="trow">
                    <td className="td font-medium text-slate-700">{f.plato}</td>
                    <td className="td text-right text-slate-600">{f.cantidadPorPorcion}</td>
                    <td className="td text-right text-slate-600">{Q(f.costoLinea)}</td>
                    <td className="td text-right text-slate-600">{Q(f.costoPlato)}</td>
                    <td className="td text-right">
                      <span className={f.porcentajeDelCosto > 50 ? 'badge-red' : 'badge-slate'}>
                        {f.porcentajeDelCosto}%
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
