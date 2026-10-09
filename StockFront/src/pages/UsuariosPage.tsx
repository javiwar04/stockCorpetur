import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarUsuario, cambiarActivoUsuario, crearUsuario, listarUsuarios, type UsuarioLista } from '../features/gestion/gestionApi';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import { useAuth } from '../features/auth/authStore';
import type { Rol } from '../features/auth/types';

const ROLES: Rol[] = ['Admin', 'Gerencia', 'Digitador', 'SoloLectura'];
const ROLES_CON_HOTELES: Rol[] = ['Digitador', 'SoloLectura'];
const PERMISOS: Record<Rol, string> = {
  Admin: 'Acceso completo a todos los establecimientos y administración de usuarios.',
  Gerencia: 'Gestión operativa, catálogos, reportes, pagos y cierres de todos los establecimientos. Sin administración de usuarios.',
  Digitador: 'Consulta y registro de compras, movimientos y conteos en los establecimientos asignados.',
  SoloLectura: 'Consulta de dashboard, compras, inventario, conteos, alertas y catálogos de los establecimientos asignados. Sin modificar datos.',
};

function mensajeError(e: unknown): string {
  const data = (e as { response?: { data?: unknown } })?.response?.data;
  if (typeof data === 'string') return data;
  if (Array.isArray(data)) return data.join(' ');
  if (data && typeof data === 'object') {
    const problema = data as { error?: string; errors?: Record<string, string[]> };
    if (problema.error) return problema.error;
    if (problema.errors) return Object.values(problema.errors).flat().join(' ');
  }
  return 'No se pudo guardar el cambio. Intenta nuevamente.';
}

export function UsuariosPage() {
  const qc = useQueryClient();
  const usuarioActual = useAuth((s) => s.usuario);
  const logout = useAuth((s) => s.logout);
  const { data: usuarios, isLoading, isError } = useQuery({ queryKey: ['usuarios'], queryFn: listarUsuarios });
  const { data: hoteles, isLoading: cargandoHoteles, isError: errorHoteles } = useQuery({
    queryKey: ['hoteles', 'usuarios'], queryFn: () => listarHoteles(false),
  });
  const formulario = useRef<HTMLDivElement>(null);
  const nombreInput = useRef<HTMLInputElement>(null);
  const [editando, setEditando] = useState<UsuarioLista | null>(null);
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rol, setRol] = useState<Rol>('Digitador');
  const [activo, setActivo] = useState(true);
  const [hotelesSel, setHotelesSel] = useState<number[]>([]);
  const [busqueda, setBusqueda] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [errorAccion, setErrorAccion] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const esMiCuenta = editando?.id === usuarioActual?.id;

  function limpiar() {
    setEditando(null);
    setNombre('');
    setEmail('');
    setPassword('');
    setRol('Digitador');
    setActivo(true);
    setHotelesSel([]);
    setError(null);
  }

  const guardarMutation = useMutation({
    mutationFn: async () => {
      const datos = { nombre: nombre.trim(), email: email.trim(), rol, hoteles: ROLES_CON_HOTELES.includes(rol) ? hotelesSel : [] };
      if (editando) return actualizarUsuario(editando.id, { ...datos, activo, password: password || undefined });
      return crearUsuario({ ...datos, password });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['usuarios'] });
      if (esMiCuenta) {
        qc.clear();
        logout();
        return;
      }
      setMensaje(editando ? 'Usuario actualizado. Deberá iniciar sesión nuevamente para usar su nuevo acceso.' : 'Usuario creado.');
      limpiar();
    },
    onError: (e: unknown) => setError(mensajeError(e)),
  });

  const activoMutation = useMutation({
    mutationFn: ({ id, activo: nuevoActivo }: { id: string; activo: boolean }) => cambiarActivoUsuario(id, nuevoActivo),
    onSuccess: (_, variables) => {
      qc.invalidateQueries({ queryKey: ['usuarios'] });
      setErrorAccion(null);
      if (editando?.id === variables.id) setActivo(variables.activo);
    },
    onError: (e: unknown) => setErrorAccion(mensajeError(e)),
  });

  function editar(u: UsuarioLista) {
    setEditando(u);
    setNombre(u.nombre);
    setEmail(u.email);
    setPassword('');
    setRol(u.roles[0] ?? 'Digitador');
    setActivo(u.activo);
    setHotelesSel([...u.hoteles]);
    setError(null);
    setMensaje(null);
    formulario.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    nombreInput.current?.focus({ preventScroll: true });
  }

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    if (guardarMutation.isPending) return;
    setError(null);
    setMensaje(null);
    if (!nombre.trim()) return setError('Ingresa el nombre del usuario.');
    if (ROLES_CON_HOTELES.includes(rol) && hotelesSel.length === 0)
      return setError('Selecciona al menos un establecimiento para este rol.');
    guardarMutation.mutate();
  };

  const nombreHotel = (id: number) => hoteles?.find((h) => h.id === id)?.nombre ?? `#${id}`;
  const filtro = busqueda.trim().toLocaleLowerCase();
  const filtrados = usuarios?.filter((u) =>
    `${u.nombre} ${u.email} ${u.roles.join(' ')} ${u.hoteles.map(nombreHotel).join(' ')}`.toLocaleLowerCase().includes(filtro));
  const ocupado = guardarMutation.isPending || activoMutation.isPending;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="page-title">Usuarios</h1>
        <p className="page-subtitle">Cuentas, roles y acceso por establecimiento.</p>
      </div>
      {mensaje && <div role="status" className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-sm text-emerald-800">{mensaje}</div>}
      <div ref={formulario} className="card card-pad scroll-mt-20 lg:scroll-mt-4">
        <h2 className="card-title mb-4">{editando ? `Editar usuario: ${editando.nombre}` : 'Nuevo usuario'}</h2>
        {error && <div role="alert" className="mb-3 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm text-rose-700">{error}</div>}
        <form onSubmit={enviar}>
          <fieldset disabled={ocupado} className="min-w-0 space-y-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
              <label className="min-w-0">
                <span className="label">Nombre completo</span>
                <input ref={nombreInput} value={nombre} onChange={(e) => setNombre(e.target.value)} required className="field w-full" autoComplete="name" />
              </label>
              <label className="min-w-0">
                <span className="label">Correo</span>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required className="field w-full" autoComplete="off" />
              </label>
              <label className="min-w-0">
                <span className="label">{editando ? 'Nueva contraseña (opcional)' : 'Contraseña'}</span>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required={!editando} minLength={8} autoComplete="new-password" className="field w-full" />
                <span className="mt-1 block text-xs text-slate-500">8+ caracteres, mayúscula, número y símbolo.</span>
              </label>
              <div className="min-w-0">
                <label htmlFor="usuario-rol" className="label">Rol</label>
                <select id="usuario-rol" value={rol} onChange={(e) => setRol(e.target.value as Rol)} disabled={esMiCuenta} className="field w-full">
                  {ROLES.map((r) => <option key={r} value={r}>{r === 'SoloLectura' ? 'Solo lectura' : r}</option>)}
                </select>
              </div>
            </div>
            <p className="text-sm text-slate-600">{PERMISOS[rol]}</p>
            {ROLES_CON_HOTELES.includes(rol) && (
              <div>
                <div className="label">Establecimientos asignados</div>
                {cargandoHoteles && <p className="text-sm text-slate-500">Cargando establecimientos...</p>}
                {errorHoteles && <p role="alert" className="text-sm text-rose-700">No se pudieron cargar los establecimientos.</p>}
                {hoteles?.length === 0 && <p className="text-sm text-slate-500">No hay establecimientos disponibles.</p>}
                <div className="flex flex-wrap gap-x-5 gap-y-3">
                  {hoteles?.map((h) => (
                    <label key={h.id} className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
                      <input type="checkbox" checked={hotelesSel.includes(h.id)} onChange={() => setHotelesSel((prev) => prev.includes(h.id) ? prev.filter((id) => id !== h.id) : [...prev, h.id])} />
                      {h.nombre}{!h.activo && ' (inactivo)'}
                    </label>
                  ))}
                </div>
              </div>
            )}
            {editando && (
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} disabled={esMiCuenta} />Usuario activo
              </label>
            )}
            {esMiCuenta && <p className="text-sm text-slate-500">Al guardar tus datos deberás iniciar sesión nuevamente. Tu cuenta conserva el rol Admin y permanece activa.</p>}
            <div className="flex flex-wrap gap-2">
              <button type="submit" disabled={ROLES_CON_HOTELES.includes(rol) && (cargandoHoteles || errorHoteles)} className="btn-primary">
                {guardarMutation.isPending ? 'Guardando...' : editando ? 'Guardar cambios' : 'Crear usuario'}
              </button>
              {editando && <button type="button" onClick={limpiar} className="btn-secondary">Cancelar</button>}
            </div>
          </fieldset>
        </form>
      </div>
      <div className="card overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 p-4">
          <h2 className="card-title">Usuarios registrados</h2>
          <input type="search" aria-label="Buscar usuarios" placeholder="Buscar nombre, correo, rol o establecimiento" value={busqueda} onChange={(e) => setBusqueda(e.target.value)} className="field w-full md:max-w-md" />
        </div>
        {errorAccion && <p role="alert" className="px-4 py-3 text-sm text-rose-700">{errorAccion}</p>}
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="thead">
              <tr>
                <th className="th">Nombre</th><th className="th">Correo</th><th className="th">Rol</th>
                <th className="th">Establecimientos</th><th className="th">Estado</th><th className="th">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && <tr><td colSpan={6} className="empty-cell">Cargando...</td></tr>}
              {isError && <tr><td colSpan={6} className="empty-cell text-rose-700">No se pudieron cargar los usuarios.</td></tr>}
              {!isLoading && !isError && filtrados?.length === 0 && <tr><td colSpan={6} className="empty-cell">{filtro ? 'No hay usuarios que coincidan con la búsqueda.' : 'No hay usuarios registrados.'}</td></tr>}
              {filtrados?.map((u) => (
                <tr key={u.id} className="trow">
                  <td className="td font-medium text-slate-700">{u.nombre}{u.id === usuarioActual?.id && <span className="ml-2 text-xs text-slate-500">(tú)</span>}</td>
                  <td className="td text-slate-500">{u.email}</td>
                  <td className="td"><span className="badge-slate">{u.roles.map((r) => r === 'SoloLectura' ? 'Solo lectura' : r).join(', ')}</span></td>
                  <td className="td text-slate-500">{u.roles.some((r) => ROLES_CON_HOTELES.includes(r)) ? u.hoteles.map(nombreHotel).join(', ') || 'Sin asignar' : 'Todos'}</td>
                  <td className="td"><span className={u.activo ? 'badge-green' : 'badge-slate'}>{u.activo ? 'Activo' : 'Inactivo'}</span></td>
                  <td className="td">
                    <div className="flex items-center justify-end gap-2 whitespace-nowrap">
                      <button type="button" disabled={ocupado} onClick={() => editar(u)} className="btn-secondary text-xs">Editar</button>
                      <button type="button" disabled={ocupado || u.id === usuarioActual?.id} onClick={() => activoMutation.mutate({ id: u.id, activo: !u.activo })} className="rounded-md px-2 py-1 text-xs font-medium text-slate-500 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50">
                        {u.activo ? 'Desactivar' : 'Activar'}
                      </button>
                    </div>
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
