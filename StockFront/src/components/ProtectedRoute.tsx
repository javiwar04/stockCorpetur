import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../features/auth/authStore';
import type { Rol } from '../features/auth/types';

interface Props {
  roles?: Rol[];
}

/** Bloquea rutas si no hay sesión, o si el rol no está permitido. */
export function ProtectedRoute({ roles }: Props) {
  const { accessToken, tieneRol } = useAuth();

  if (!accessToken) return <Navigate to="/login" replace />;
  if (roles && roles.length > 0 && !tieneRol(...roles))
    return <Navigate to="/" replace />;

  return <Outlet />;
}
