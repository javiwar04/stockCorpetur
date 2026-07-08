import { lazy, Suspense } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './components/ProtectedRoute';

const LoginPage = lazy(() => import('./pages/LoginPage').then((m) => ({ default: m.LoginPage })));
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const DocumentosPage = lazy(() => import('./pages/DocumentosPage').then((m) => ({ default: m.DocumentosPage })));
const InventarioPage = lazy(() => import('./pages/InventarioPage').then((m) => ({ default: m.InventarioPage })));
const ConteosPage = lazy(() => import('./pages/ConteosPage').then((m) => ({ default: m.ConteosPage })));
const AlertasPage = lazy(() => import('./pages/AlertasPage').then((m) => ({ default: m.AlertasPage })));
const CierresPage = lazy(() => import('./pages/CierresPage').then((m) => ({ default: m.CierresPage })));
const AuditoriaPage = lazy(() => import('./pages/AuditoriaPage').then((m) => ({ default: m.AuditoriaPage })));
const ProductosPage = lazy(() => import('./pages/ProductosPage').then((m) => ({ default: m.ProductosPage })));
const ProveedoresPage = lazy(() => import('./pages/ProveedoresPage').then((m) => ({ default: m.ProveedoresPage })));
const ReportesPage = lazy(() => import('./pages/ReportesPage').then((m) => ({ default: m.ReportesPage })));
const CuentasPorPagarPage = lazy(() => import('./pages/CuentasPorPagarPage').then((m) => ({ default: m.CuentasPorPagarPage })));
const GestionPage = lazy(() => import('./pages/GestionPage').then((m) => ({ default: m.GestionPage })));
const PlatosPage = lazy(() => import('./pages/PlatosPage').then((m) => ({ default: m.PlatosPage })));
const UsuariosPage = lazy(() => import('./pages/UsuariosPage').then((m) => ({ default: m.UsuariosPage })));

function PageFallback() {
  return (
    <div className="grid min-h-screen place-items-center bg-slate-50 text-sm font-medium text-slate-500">
      Cargando…
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<PageFallback />}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          <Route element={<ProtectedRoute />}>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route path="documentos" element={<DocumentosPage />} />
              <Route path="inventario" element={<InventarioPage />} />
              <Route path="conteos" element={<ConteosPage />} />
              <Route path="alertas" element={<AlertasPage />} />
              <Route path="productos" element={<ProductosPage />} />
              <Route path="proveedores" element={<ProveedoresPage />} />
            </Route>
          </Route>

          <Route element={<ProtectedRoute roles={['Admin', 'Gerencia']} />}>
            <Route element={<Layout />}>
              <Route path="reportes" element={<ReportesPage />} />
              <Route path="cuentas-por-pagar" element={<CuentasPorPagarPage />} />
              <Route path="cierres" element={<CierresPage />} />
              <Route path="auditoria" element={<AuditoriaPage />} />
              <Route path="gestion" element={<GestionPage />} />
              <Route path="platos" element={<PlatosPage />} />
            </Route>
          </Route>

          <Route element={<ProtectedRoute roles={['Admin']} />}>
            <Route element={<Layout />}>
              <Route path="usuarios" element={<UsuariosPage />} />
            </Route>
          </Route>
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
