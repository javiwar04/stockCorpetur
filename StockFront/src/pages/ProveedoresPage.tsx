import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarProveedor, crearProveedor, listarProveedores } from '../features/catalogos/catalogosApi';
import type { Proveedor } from '../features/catalogos/types';
import { useAuth } from '../features/auth/authStore';

export function ProveedoresPage() {
  const qc = useQueryClient();
  const puedeEditar = useAuth((s) => s.tieneRol('Admin', 'Gerencia'));

  const { data: proveedores, isLoading } = useQuery({
    queryKey: ['proveedores'],
    queryFn: () => listarProveedores(false),
  });

  const [nombre, setNombre] = useState('');
  const [nit, setNit] = useState('');
  const [telefono, setTelefono] = useState('');
  const [diasCredito, setDiasCredito] = useState('0');
  const [editando, setEditando] = useState<Proveedor | null>(null);

  const crearMutation = useMutation({
    mutationFn: crearProveedor,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['proveedores'] });
      setNombre('');
      setNit('');
      setTelefono('');
      setDiasCredito('0');
    },
  });

  const actualizarMutation = useMutation({
    mutationFn: (p: Proveedor) =>
      actualizarProveedor(p.id, {
        nombre: p.nombre,
        nit: p.nit ?? undefined,
        telefono: p.telefono ?? undefined,
        diasCredito: p.diasCredito,
        activo: p.activo,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['proveedores'] });
      setEditando(null);
    },
  });

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    crearMutation.mutate({
      nombre,
      nit: nit || undefined,
      telefono: telefono || undefined,
      diasCredito: Number(diasCredito) || 0,
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Proveedores</h1>
        <p className="page-subtitle">Proveedores, datos fiscales y condiciones de credito.</p>
      </div>

      {puedeEditar && (
        <div className="card card-pad">
          <h2 className="card-title mb-4">Nuevo proveedor</h2>
          <form onSubmit={enviar} className="flex flex-wrap items-end gap-3">
            <div className="min-w-44 flex-1">
              <label className="label">Nombre</label>
              <input value={nombre} onChange={(e) => setNombre(e.target.value)} required className="field" />
            </div>
            <div>
              <label className="label">NIT</label>
              <input value={nit} onChange={(e) => setNit(e.target.value)} className="field" />
            </div>
            <div>
              <label className="label">Telefono</label>
              <input value={telefono} onChange={(e) => setTelefono(e.target.value)} className="field" />
            </div>
            <div>
              <label className="label">Dias credito</label>
              <input
                type="number"
                min="0"
                value={diasCredito}
                onChange={(e) => setDiasCredito(e.target.value)}
                className="field w-28"
              />
            </div>
            <button type="submit" disabled={crearMutation.isPending} className="btn-primary">
              Agregar
            </button>
          </form>
        </div>
      )}

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Nombre</th>
                <th className="th">NIT</th>
                <th className="th">Telefono</th>
                <th className="th text-right">Credito</th>
                <th className="th">Estado</th>
                {puedeEditar && <th className="th text-right">Acciones</th>}
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={puedeEditar ? 6 : 5} className="empty-cell">Cargando...</td>
                </tr>
              )}
              {proveedores?.map((p) => {
                const enEdicion = editando?.id === p.id;
                return (
                  <tr key={p.id} className="trow">
                    <td className="td font-medium text-slate-700">
                      {enEdicion ? (
                        <input
                          value={editando.nombre}
                          onChange={(e) => setEditando({ ...editando, nombre: e.target.value })}
                          className="field"
                        />
                      ) : (
                        p.nombre
                      )}
                    </td>
                    <td className="td text-slate-600">
                      {enEdicion ? (
                        <input
                          value={editando.nit ?? ''}
                          onChange={(e) => setEditando({ ...editando, nit: e.target.value || null })}
                          className="field"
                        />
                      ) : (
                        p.nit ?? '-'
                      )}
                    </td>
                    <td className="td text-slate-600">
                      {enEdicion ? (
                        <input
                          value={editando.telefono ?? ''}
                          onChange={(e) => setEditando({ ...editando, telefono: e.target.value || null })}
                          className="field"
                        />
                      ) : (
                        p.telefono ?? '-'
                      )}
                    </td>
                    <td className="td text-right text-slate-600">
                      {enEdicion ? (
                        <input
                          type="number"
                          min="0"
                          value={editando.diasCredito}
                          onChange={(e) => setEditando({ ...editando, diasCredito: Number(e.target.value) || 0 })}
                          className="field w-24 text-right"
                        />
                      ) : (
                        `${p.diasCredito} dias`
                      )}
                    </td>
                    <td className="td">
                      {enEdicion ? (
                        <label className="inline-flex items-center gap-2 text-xs text-slate-600">
                          <input
                            type="checkbox"
                            checked={editando.activo}
                            onChange={(e) => setEditando({ ...editando, activo: e.target.checked })}
                          />
                          Activo
                        </label>
                      ) : (
                        <span className={p.activo ? 'badge-green' : 'badge-slate'}>{p.activo ? 'Activo' : 'Inactivo'}</span>
                      )}
                    </td>
                    {puedeEditar && (
                      <td className="td whitespace-nowrap text-right">
                        {enEdicion ? (
                          <>
                            <button
                              type="button"
                              onClick={() => actualizarMutation.mutate(editando)}
                              disabled={actualizarMutation.isPending}
                              className="mr-1 rounded-md px-2 py-1 text-xs font-medium text-emerald-600 transition-colors hover:bg-emerald-50"
                            >
                              Guardar
                            </button>
                            <button
                              type="button"
                              onClick={() => setEditando(null)}
                              className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100"
                            >
                              Cancelar
                            </button>
                          </>
                        ) : (
                          <button
                            type="button"
                            onClick={() => setEditando(p)}
                            className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                          >
                            Editar
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
              {proveedores?.length === 0 && (
                <tr>
                  <td colSpan={puedeEditar ? 6 : 5} className="empty-cell">Sin proveedores registrados aun.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
