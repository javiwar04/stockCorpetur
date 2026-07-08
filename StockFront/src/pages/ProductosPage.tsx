import { Fragment, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearProducto,
  crearUnidad,
  listarProductos,
  listarUnidades,
} from '../features/catalogos/catalogosApi';
import { CATEGORIAS } from '../features/catalogos/types';
import { useAuth } from '../features/auth/authStore';
import { ConversionesPanel } from '../components/ConversionesPanel';

export function ProductosPage() {
  const qc = useQueryClient();
  const puedeEditar = useAuth((s) => s.tieneRol('Admin', 'Gerencia'));
  const [expandido, setExpandido] = useState<number | null>(null);

  const { data: productos, isLoading } = useQuery({
    queryKey: ['productos'],
    queryFn: () => listarProductos(false),
  });
  const { data: unidades } = useQuery({ queryKey: ['unidades'], queryFn: listarUnidades });

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
      setError(null);
    },
    onError: () => setError('No se pudo crear el producto. Verifica los datos.'),
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
              {productos?.map((p) => (
                <Fragment key={p.id}>
                  <tr className="trow">
                    <td className="td font-medium text-slate-700">{p.nombre}</td>
                    <td className="td text-slate-600">{p.categoria}</td>
                    <td className="td text-slate-600">{p.unidadBaseNombre}</td>
                    <td className="td">
                      <span className={p.activo ? 'badge-green' : 'badge-slate'}>{p.activo ? 'Activo' : 'Inactivo'}</span>
                    </td>
                    <td className="td text-right">
                      <button
                        onClick={() => setExpandido((prev) => (prev === p.id ? null : p.id))}
                        className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                      >
                        {expandido === p.id ? 'Cerrar' : 'Unidades'}
                      </button>
                    </td>
                  </tr>
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
              {productos?.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty-cell">Sin productos registrados aún.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
