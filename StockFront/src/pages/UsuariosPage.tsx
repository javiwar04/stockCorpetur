import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cambiarActivoUsuario,
  crearUsuario,
  listarUsuarios,
} from '../features/gestion/gestionApi';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import type { Rol } from '../features/auth/types';

const ROLES: Rol[] = ['Admin', 'Gerencia', 'Digitador', 'SoloLectura'];
const ROLES_CON_HOTELES: Rol[] = ['Digitador', 'SoloLectura'];

export function UsuariosPage() {
  const qc = useQueryClient();
  const { data: usuarios, isLoading } = useQuery({ queryKey: ['usuarios'], queryFn: listarUsuarios });
  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });

  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rol, setRol] = useState<Rol>('Digitador');
  const [hotelesSel, setHotelesSel] = useState<number[]>([]);
  const [error, setError] = useState<string | null>(null);

  const crearMutation = useMutation({
    mutationFn: crearUsuario,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['usuarios'] });
      setNombre('');
      setEmail('');
      setPassword('');
      setHotelesSel([]);
      setError(null);
    },
    onError: (e: unknown) => {
      const data = (e as { response?: { data?: unknown } })?.response?.data;
      setError(Array.isArray(data) ? data.join(' ') : 'No se pudo crear el usuario. Revisa los datos.');
    },
  });

  const activoMutation = useMutation({
    mutationFn: ({ id, activo }: { id: string; activo: boolean }) => cambiarActivoUsuario(id, activo),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['usuarios'] }),
  });

  const alternarHotel = (id: number) =>
    setHotelesSel((prev) => (prev.includes(id) ? prev.filter((h) => h !== id) : [...prev, id]));

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    if (ROLES_CON_HOTELES.includes(rol) && hotelesSel.length === 0)
      return setError(`El rol ${rol} necesita al menos un hotel asignado.`);
    crearMutation.mutate({
      nombre,
      email,
      password,
      rol,
      hoteles: ROLES_CON_HOTELES.includes(rol) ? hotelesSel : undefined,
    });
  };

  const nombreHotel = (id: number) => hoteles?.find((h) => h.id === id)?.nombre ?? `#${id}`;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Usuarios</h1>
        <p className="page-subtitle">Cuentas y permisos del sistema.</p>
      </div>

      <div className="card card-pad">
        <h2 className="card-title mb-4">Nuevo usuario</h2>
        {error && (
          <div className="mb-3 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm text-rose-700">{error}</div>
        )}
        <form onSubmit={enviar} className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <input
              placeholder="Nombre completo"
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              required
              className="field min-w-44 flex-1"
            />
            <input
              type="email"
              placeholder="Correo"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="field min-w-44 flex-1"
            />
            <input
              type="password"
              placeholder="Contraseña (8+ car., mayús., número, símbolo)"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              className="field min-w-60 flex-1"
            />
            <select value={rol} onChange={(e) => setRol(e.target.value as Rol)} className="field w-auto">
              {ROLES.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </div>

          {ROLES_CON_HOTELES.includes(rol) && (
            <div>
              <div className="label">Hoteles asignados</div>
              <div className="flex flex-wrap gap-2">
                {hoteles?.map((h) => (
                  <label
                    key={h.id}
                    className={`cursor-pointer rounded-full px-3 py-1.5 text-xs font-medium ring-1 transition-colors ${
                      hotelesSel.includes(h.id)
                        ? 'bg-slate-900 text-white ring-slate-900'
                        : 'bg-white text-slate-600 ring-slate-300 hover:ring-slate-400'
                    }`}
                  >
                    <input
                      type="checkbox"
                      className="hidden"
                      checked={hotelesSel.includes(h.id)}
                      onChange={() => alternarHotel(h.id)}
                    />
                    {h.nombre}
                  </label>
                ))}
              </div>
            </div>
          )}

          <button type="submit" disabled={crearMutation.isPending} className="btn-primary">
            {crearMutation.isPending ? 'Creando…' : 'Crear usuario'}
          </button>
        </form>
      </div>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Nombre</th>
                <th className="th">Correo</th>
                <th className="th">Rol</th>
                <th className="th">Hoteles</th>
                <th className="th">Estado</th>
                <th className="th"></th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={6} className="empty-cell">Cargando…</td>
                </tr>
              )}
              {usuarios?.map((u) => (
                <tr key={u.id} className="trow">
                  <td className="td font-medium text-slate-700">{u.nombre}</td>
                  <td className="td text-slate-500">{u.email}</td>
                  <td className="td">
                    <span className="badge-slate">{u.roles.join(', ')}</span>
                  </td>
                  <td className="td text-slate-500">
                    {u.roles.some((r) => ROLES_CON_HOTELES.includes(r)) ? u.hoteles.map(nombreHotel).join(', ') || '—' : 'Todos'}
                  </td>
                  <td className="td">
                    <span className={u.activo ? 'badge-green' : 'badge-slate'}>{u.activo ? 'Activo' : 'Inactivo'}</span>
                  </td>
                  <td className="td text-right">
                    <button
                      onClick={() => activoMutation.mutate({ id: u.id, activo: !u.activo })}
                      className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800"
                    >
                      {u.activo ? 'Desactivar' : 'Activar'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
