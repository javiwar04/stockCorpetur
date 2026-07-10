import { Fragment, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarProducto,
  crearProducto,
  crearUnidad,
  listarProductos,
  listarUnidades,
} from '../features/catalogos/catalogosApi';
import { CATEGORIAS } from '../features/catalogos/types';
import type { Producto } from '../features/catalogos/types';
import { useAuth } from '../features/auth/authStore';
import { ConversionesPanel } from '../components/ConversionesPanel';

type ProductoEdicion = {
  nombre: string;
  categoria: string;
  unidadBaseId: number | '';
  activo: boolean;
};

export function ProductosPage() {
  const qc = useQueryClient();
  const puedeEditar = useAuth((s) => s.tieneRol('Admin', 'Gerencia'));
  const [expandido, setExpandido] = useState<number | null>(null);
  const [busqueda, setBusqueda] = useState('');
  const [editandoId, setEditandoId] = useState<number | null>(null);
  const [edicion, setEdicion] = useState<ProductoEdicion | null>(null);

  const { data: productos, isLoading } = useQuery({
    queryKey: ['productos'],
    queryFn: () => listarProductos(false),
  });
  const { data: unidades } = useQuery({ queryKey: ['unidades'], queryFn: listarUnidades });

  const productosFiltrados = useMemo(() => {
    const texto = busqueda.trim().toLowerCase();
    if (!texto) return productos ?? [];
    return (productos ?? []).filter((p) =>
      [p.nombre, p.categoria, p.unidadBaseNombre, p.activo ? 'activo' : 'inactivo'].some((valor) =>
        valor.toLowerCase().includes(texto),
      ),
    );
  }, [productos, busqueda]);

  const [nombre, setNombre] = useState('');
  const [categoria, setCategoria] = useState<string>(CATEGORIAS[0]);
  const [unidadBaseId, setUnidadBaseId] = useState<number | ''>('');
  const [error, setError] = useState<string | null>(null);

  const [nuevaUnidad, setNuevaUnidad] = useState('');
  const [nuevaUnidadAbrev, setNuevaUnidadAbrev] = useState('');

  const crearMutation = useMutation({
    mutationFn: crearProducto,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['productos'] });
      setNombre('');
      setUnidadBaseId('');
      setError(null);
    },
    onError: () => setError('No se pudo crear el producto. Verifica los datos.'),
  });

  const actualizarMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { nombre: string; categoria: string; unidadBaseId: number; activo: boolean } }) =>
      actualizarProducto(id, data),
    onSuccess: (producto) => {
      qc.invalidateQueries({ queryKey: ['productos'] });
      qc.invalidateQueries({ queryKey: ['conversiones', producto.id] });
      setEditandoId(null);
      setEdicion(null);
      setError(null);
    },
    onError: () => setError('No se pudo actualizar el producto. Revisa nombre y unidad.'),
  });

  const crearUnidadMutation = useMutation({
    mutationFn: crearUnidad,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['unidades'] });
      setNuevaUnidad('');
      setNuevaUnidadAbrev('');
    },
  });

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    if (!unidadBaseId) return setError('Selecciona la unidad base.');
    crearMutation.mutate({ nombre, categoria, unidadBaseId });
  };

  const iniciarEdicion = (producto: Producto) => {
    setEditandoId(producto.id);
    setEdicion({
      nombre: producto.nombre,
      categoria: producto.categoria,
      unidadBaseId: producto.unidadBaseId,
      activo: producto.activo,
    });
    setExpandido(null);
    setError(null);
  };

  const guardarEdicion = (id: number) => {
    if (!edicion) return;
    if (!edicion.nombre.trim()) return setError('El nombre del producto es obligatorio.');
    if (!edicion.unidadBaseId) return setError('Selecciona la unidad base.');

    actualizarMutation.mutate({
      id,
      data: {
        nombre: edicion.nombre,
        categoria: edicion.categoria,
        unidadBaseId: Number(edicion.unidadBaseId),
        activo: edicion.activo,
      },
    });
  };

  const cancelarEdicion = () => {
    setEditandoId(null);
    setEdicion(null);
    setError(null);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Productos</h1>
        <p className="page-subtitle">Catálogo de vegetales, frutas, condimentos, lácteos y proteínas.</p>
      </div>

      {puedeEditar && (
        <div className="card card-pad">
          <h2 className="card-title mb-4">Nuevo producto</h2>
          {error && <p className="mb-3 text-sm text-rose-600">{error}</p>}
          <form onSubmit={enviar} className="flex flex-wrap items-end gap-3">
            <div className="min-w-44 flex-1">
              <label className="label">Nombre</label>
              <input value={nombre} onChange={(e) => setNombre(e.target.value)} required className="field" />
            </div>
            <div>
              <label className="label">Categoría</label>
              <select value={categoria} onChange={(e) => setCategoria(e.target.value)} className="field">
                {CATEGORIAS.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label">Unidad base</label>
              <select value={unidadBaseId} onChange={(e) => setUnidadBaseId(Number(e.target.value))} className="field">
                <option value="">Selecciona…</option>
                {unidades?.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.nombre} ({u.abreviatura})
                  </option>
                ))}
              </select>
            </div>
            <button type="submit" disabled={crearMutation.isPending} className="btn-primary">
              Agregar
            </button>
          </form>

          <details className="mt-4">
            <summary className="cursor-pointer text-xs font-medium text-slate-500 hover:text-slate-700">
              ¿Falta una unidad de medida?
            </summary>
            <div className="mt-3 flex flex-wrap gap-2">
              <input
                placeholder="Nombre (ej. Malla)"
                value={nuevaUnidad}
                onChange={(e) => setNuevaUnidad(e.target.value)}
                className="field w-auto"
              />
              <input
                placeholder="Abrev. (ej. mll)"
                value={nuevaUnidadAbrev}
                onChange={(e) => setNuevaUnidadAbrev(e.target.value)}
                className="field w-28"
              />
              <button
                type="button"
                onClick={() => crearUnidadMutation.mutate({ nombre: nuevaUnidad, abreviatura: nuevaUnidadAbrev })}
                disabled={!nuevaUnidad || !nuevaUnidadAbrev}
                className="btn-secondary btn-sm"
              >
                Crear unidad
              </button>
            </div>
          </details>
        </div>
      )}

      <div className="card overflow-hidden">
        <div className="card-header flex-wrap">
          <div>
            <h2 className="card-title">Catálogo de productos</h2>
            <p className="mt-1 text-xs text-slate-500">
              {productosFiltrados.length} visibles de {productos?.length ?? 0} productos registrados.
            </p>
          </div>
          <div className="w-full sm:w-80">
            <label className="label">Buscar</label>
            <input
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
              placeholder="Nombre, categoría, unidad o estado"
              className="field"
            />
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Nombre</th>
                <th className="th">Categoría</th>
                <th className="th">Unidad base</th>
                <th className="th">Estado</th>
                <th className="th"></th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={5} className="empty-cell">Cargando…</td>
                </tr>
              )}
              {productosFiltrados.map((p) => (
                <Fragment key={p.id}>
                  {editandoId === p.id && edicion ? (
                    <tr className="border-t border-emerald-100 bg-emerald-50/40 align-top">
                      <td className="td">
                        <label className="label">Nombre</label>
                        <input
                          value={edicion.nombre}
                          onChange={(e) => setEdicion({ ...edicion, nombre: e.target.value })}
                          className="field bg-white"
                        />
                      </td>
                      <td className="td">
                        <label className="label">Categoría</label>
                        <select
                          value={edicion.categoria}
                          onChange={(e) => setEdicion({ ...edicion, categoria: e.target.value })}
                          className="field bg-white"
                        >
                          {CATEGORIAS.map((c) => (
                            <option key={c} value={c}>
                              {c}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="td">
                        <label className="label">Unidad base</label>
                        <select
                          value={edicion.unidadBaseId}
                          onChange={(e) =>
                            setEdicion({ ...edicion, unidadBaseId: e.target.value === '' ? '' : Number(e.target.value) })
                          }
                          className="field bg-white"
                        >
                          <option value="">Selecciona…</option>
                          {unidades?.map((u) => (
                            <option key={u.id} value={u.id}>
                              {u.nombre} ({u.abreviatura})
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="td">
                        <label className="label">Estado</label>
                        <label className="inline-flex items-center gap-2 rounded-lg bg-white px-3 py-2 text-sm text-slate-700 ring-1 ring-slate-200">
                          <input
                            type="checkbox"
                            checked={edicion.activo}
                            onChange={(e) => setEdicion({ ...edicion, activo: e.target.checked })}
                            className="h-4 w-4 rounded border-slate-300 text-emerald-600 focus:ring-emerald-500"
                          />
                          Activo
                        </label>
                      </td>
                      <td className="td text-right">
                        <div className="flex flex-wrap justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => guardarEdicion(p.id)}
                            disabled={actualizarMutation.isPending}
                            className="btn-primary btn-sm"
                          >
                            Guardar
                          </button>
                          <button type="button" onClick={cancelarEdicion} className="btn-secondary btn-sm">
                            Cancelar
                          </button>
                        </div>
                      </td>
                    </tr>
                  ) : (
                    <tr className="trow">
                      <td className="td font-medium text-slate-700">{p.nombre}</td>
                      <td className="td text-slate-600">{p.categoria}</td>
                      <td className="td text-slate-600">{p.unidadBaseNombre}</td>
                      <td className="td">
                        <span className={p.activo ? 'badge-green' : 'badge-slate'}>{p.activo ? 'Activo' : 'Inactivo'}</span>
                      </td>
                      <td className="td text-right">
                        <div className="flex flex-wrap justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => setExpandido((prev) => (prev === p.id ? null : p.id))}
                            className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                          >
                            {expandido === p.id ? 'Cerrar' : 'Unidades'}
                          </button>
                          {puedeEditar && (
                            <button
                              type="button"
                              onClick={() => iniciarEdicion(p)}
                              className="rounded-md px-2 py-1 text-xs font-medium text-emerald-600 transition-colors hover:bg-emerald-50"
                            >
                              Editar
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                  {expandido === p.id && (
                    <tr>
                      <td colSpan={5} className="px-5 pb-4">
                        <ConversionesPanel
                          productoId={p.id}
                          unidadBaseNombre={p.unidadBaseNombre}
                          puedeEditar={puedeEditar}
                        />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {!isLoading && productosFiltrados.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty-cell">
                    {productos?.length === 0 ? 'Sin productos registrados aún.' : 'No hay productos con esa búsqueda.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
