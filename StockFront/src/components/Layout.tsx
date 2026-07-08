import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { obtenerResumenAlertas } from '../features/alertas/alertasApi';
import { useAuth } from '../features/auth/authStore';
import type { Rol } from '../features/auth/types';

interface NavItem {
  to: string;
  label: string;
  roles?: Rol[];
}

const NAV: NavItem[] = [
  { to: '/', label: 'Dashboard' },
  { to: '/documentos', label: 'Documentos' },
  { to: '/inventario', label: 'Inventario' },
  { to: '/conteos', label: 'Conteos' },
  { to: '/alertas', label: 'Alertas' },
  { to: '/productos', label: 'Productos' },
  { to: '/proveedores', label: 'Proveedores' },
  { to: '/platos', label: 'Menú y recetas', roles: ['Admin', 'Gerencia'] },
  { to: '/reportes', label: 'Reportes', roles: ['Admin', 'Gerencia'] },
  { to: '/cuentas-por-pagar', label: 'Cuentas por pagar', roles: ['Admin', 'Gerencia'] },
  { to: '/cierres', label: 'Cierre mensual', roles: ['Admin', 'Gerencia'] },
  { to: '/auditoria', label: 'Auditoria', roles: ['Admin', 'Gerencia'] },
  { to: '/gestion', label: 'Gestión mensual', roles: ['Admin', 'Gerencia'] },
  { to: '/usuarios', label: 'Usuarios', roles: ['Admin'] },
];

const LOGO_SISTEMA = '/logoSistema%28Paralogin%20y%20favicon%29.png';

// Ícono outline por ruta (solo presentación).
function NavIcon({ to }: { to: string }) {
  const paths: Record<string, string> = {
    '/': 'M3.75 6A2.25 2.25 0 016 3.75h2.25A2.25 2.25 0 0110.5 6v2.25a2.25 2.25 0 01-2.25 2.25H6A2.25 2.25 0 013.75 8.25V6zm0 9.75A2.25 2.25 0 016 13.5h2.25a2.25 2.25 0 012.25 2.25V18a2.25 2.25 0 01-2.25 2.25H6A2.25 2.25 0 013.75 18v-2.25zm9.75-9.75A2.25 2.25 0 0115.75 3.75H18A2.25 2.25 0 0120.25 6v2.25A2.25 2.25 0 0118 10.5h-2.25a2.25 2.25 0 01-2.25-2.25V6zm0 9.75a2.25 2.25 0 012.25-2.25H18a2.25 2.25 0 012.25 2.25V18A2.25 2.25 0 0118 20.25h-2.25A2.25 2.25 0 0113.5 18v-2.25z',
    '/documentos': 'M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z',
    '/inventario': 'M20.25 7.5l-.625 10.632a2.25 2.25 0 01-2.247 2.118H6.622a2.25 2.25 0 01-2.247-2.118L3.75 7.5m8.25 3v6.75m0 0l-3-3m3 3l3-3M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-1.5c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v1.5c0 .621.504 1.125 1.125 1.125z',
    '/conteos': 'M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
    '/alertas': 'M12 9v3.75m0 3h.008v.008H12V15.75M10.29 3.86 1.82 18a1.5 1.5 0 001.29 2.25h17.78A1.5 1.5 0 0022.18 18L13.71 3.86a1.5 1.5 0 00-3.42 0z',
    '/productos': 'M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z M6 6h.008v.008H6V6z',
    '/proveedores': 'M8.25 18.75a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h6m-9 0H3.375a1.125 1.125 0 01-1.125-1.125V14.25m17.25 4.5a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h1.125c.621 0 1.129-.504 1.09-1.124a17.902 17.902 0 00-3.213-9.193 2.056 2.056 0 00-1.58-.86H14.25M16.5 18.75h-2.25m0-11.177v-.958c0-.568-.422-1.048-.987-1.106a48.554 48.554 0 00-10.026 0 1.106 1.106 0 00-.987 1.106v7.635m12-6.677v6.677m0 4.5v-4.5m0 0h-12',
    '/platos': 'M12 6.042A8.967 8.967 0 006 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 016 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 016-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0018 18a8.967 8.967 0 00-6 2.292m0-14.25v14.25',
    '/reportes': 'M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 013 19.875v-6.75zM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V8.625zM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V4.125z',
    '/cuentas-por-pagar': 'M2.25 18.75a2.25 2.25 0 002.25 2.25h15a2.25 2.25 0 002.25-2.25V8.25a2.25 2.25 0 00-2.25-2.25h-15a2.25 2.25 0 00-2.25 2.25v10.5zM2.25 9h19.5M6.75 15.75h3m3 0h4.5',
    '/cierres': 'M6.75 3v2.25M17.25 3v2.25M4.5 8.25h15M6.75 12.75l2.25 2.25 4.5-4.5M5.25 5.25h13.5A2.25 2.25 0 0121 7.5v11.25A2.25 2.25 0 0118.75 21H5.25A2.25 2.25 0 013 18.75V7.5A2.25 2.25 0 015.25 5.25z',
    '/auditoria': 'M12 3.75 4.5 6.75v5.25c0 4.556 3.075 8.814 7.5 9.75 4.425-.936 7.5-5.194 7.5-9.75V6.75L12 3.75zM9.75 12.75 11.25 14.25 15 10.5',
    '/gestion': 'M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 012.25-2.25h13.5A2.25 2.25 0 0121 7.5v11.25m-18 0A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75m-18 0v-7.5A2.25 2.25 0 015.25 9h13.5A2.25 2.25 0 0121 11.25v7.5',
    '/usuarios': 'M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z',
  };
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-5 w-5 shrink-0"
      aria-hidden="true"
    >
      <path d={paths[to] ?? paths['/']} />
    </svg>
  );
}

export function Layout() {
  const { usuario, tieneRol, logout } = useAuth();
  const navigate = useNavigate();
  const [menuAbierto, setMenuAbierto] = useState(false);
  const { data: resumenAlertas } = useQuery({
    queryKey: ['alertas-resumen'],
    queryFn: obtenerResumenAlertas,
    refetchInterval: 60000,
  });
  const totalAlertas = resumenAlertas?.total ?? 0;

  const salir = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const visibles = NAV.filter((n) => !n.roles || tieneRol(...n.roles));

  const iniciales = (usuario?.nombre ?? '?')
    .split(' ')
    .map((p) => p[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  const barra = (
    <div
      className="flex h-full flex-col bg-slate-950 text-slate-300"
      style={{ backgroundImage: 'radial-gradient(135% 55% at 50% 0%, rgba(16,185,129,0.12), rgba(2,6,23,0) 58%)' }}
    >
      <div className="flex items-center gap-3 px-5 py-5">
        <div className="grid h-10 w-10 place-items-center rounded-xl bg-white p-1 shadow-sm ring-1 ring-white/10">
          <img src={LOGO_SISTEMA} alt="" className="h-full w-full rounded-lg object-contain" />
        </div>
        <div>
          <div className="text-[15px] font-semibold tracking-tight text-white">StockControl</div>
          <div className="text-[11px] text-cyan-200/75">Compras e inventario</div>
        </div>
      </div>

      <nav className="flex-1 space-y-0.5 overflow-y-auto px-3 py-2">
        {visibles.map((n) => (
          <NavLink
            key={n.to}
            to={n.to}
            end={n.to === '/'}
            onClick={() => setMenuAbierto(false)}
            className={({ isActive }) =>
              `group relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors ${
                isActive ? 'bg-white/[0.06] font-medium text-white' : 'text-slate-400 hover:bg-white/[0.04] hover:text-white'
              }`
            }
          >
            {({ isActive }) => (
              <>
                <span
                  className={`absolute -left-3 top-1/2 h-6 w-1 -translate-y-1/2 rounded-r-full bg-emerald-400 transition-opacity ${
                    isActive ? 'opacity-100' : 'opacity-0'
                  }`}
                />
                <span className={isActive ? 'text-emerald-400' : 'text-slate-500 transition-colors group-hover:text-slate-300'}>
                  <NavIcon to={n.to} />
                </span>
                <span className="min-w-0 flex-1 truncate">{n.label}</span>
                {n.to === '/alertas' && totalAlertas > 0 && (
                  <span className="rounded-full bg-rose-500 px-2 py-0.5 text-[11px] font-semibold text-white">
                    {totalAlertas > 99 ? '99+' : totalAlertas}
                  </span>
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="border-t border-white/10 p-3">
        <div className="flex items-center gap-3 rounded-xl px-2 py-2">
          <div className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-emerald-500/15 text-xs font-semibold text-emerald-300 ring-1 ring-emerald-400/20">
            {iniciales}
          </div>
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm font-medium text-white">{usuario?.nombre}</div>
            <div className="truncate text-[11px] text-slate-400">{usuario?.roles.join(', ')}</div>
          </div>
          <button
            onClick={salir}
            title="Cerrar sesión"
            className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-white/10 hover:text-white"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.6} className="h-5 w-5">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M15.75 9V5.25A2.25 2.25 0 0013.5 3h-6a2.25 2.25 0 00-2.25 2.25v13.5A2.25 2.25 0 007.5 21h6a2.25 2.25 0 002.25-2.25V15M12 9l3 3m0 0l-3 3m3-3H2.25"
              />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );

  return (
    <div className="flex min-h-screen">
      {/* Sidebar fijo (desktop) */}
      <aside className="hidden w-64 shrink-0 lg:block">
        <div className="sticky top-0 h-screen">{barra}</div>
      </aside>

      {/* Drawer (móvil) */}
      {menuAbierto && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-950/50 backdrop-blur-sm" onClick={() => setMenuAbierto(false)} />
          <aside className="absolute left-0 top-0 h-full w-64 shadow-xl">{barra}</aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        {/* Topbar (móvil) */}
        <header className="sticky top-0 z-30 flex items-center gap-3 border-b border-slate-200/70 bg-white/80 px-4 py-3 backdrop-blur lg:hidden">
          <button
            onClick={() => setMenuAbierto(true)}
            className="rounded-lg p-2 text-slate-600 transition-colors hover:bg-slate-100"
            aria-label="Abrir menú"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-6 w-6">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5M3.75 17.25h16.5" />
            </svg>
          </button>
          <img src={LOGO_SISTEMA} alt="" className="h-8 w-8 rounded-lg object-contain ring-1 ring-slate-200/70" />
          <span className="text-sm font-semibold text-slate-800">StockControl</span>
        </header>

        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
