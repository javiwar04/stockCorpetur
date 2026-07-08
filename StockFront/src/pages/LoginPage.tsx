import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { login } from '../features/auth/authApi';
import { useAuth } from '../features/auth/authStore';

const FONDO_LOGIN = '/fondo%20login.png';
const LOGO_SISTEMA = '/logoSistema%28Paralogin%20y%20favicon%29.png';

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);
  const setSesion = useAuth((s) => s.setSesion);
  const navigate = useNavigate();

  const enviar = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setCargando(true);
    try {
      const res = await login(email, password);
      setSesion(res.accessToken, res.refreshToken, res.usuario);
      navigate('/', { replace: true });
    } catch {
      setError('Credenciales inválidas. Verifica tu correo y contraseña.');
    } finally {
      setCargando(false);
    }
  };

  return (
    <main className="relative min-h-screen overflow-hidden bg-sky-50">
      <div
        className="absolute inset-0 bg-cover bg-left sm:bg-center"
        style={{ backgroundImage: `url("${FONDO_LOGIN}")` }}
        aria-hidden="true"
      />
      <div className="absolute inset-0 bg-white/45 lg:bg-white/10" aria-hidden="true" />

      <div className="relative flex min-h-screen items-center justify-center px-4 py-8 sm:px-6 lg:justify-end lg:px-[7vw]">
        <section className="w-full max-w-md rounded-2xl bg-white/88 p-5 shadow-2xl shadow-slate-900/10 ring-1 ring-white/80 backdrop-blur-md sm:p-7">
          <div className="mb-7 flex flex-col items-center text-center">
            <img
              src={LOGO_SISTEMA}
              alt="StockControl"
              className="h-24 w-24 rounded-2xl object-contain shadow-sm ring-1 ring-slate-200/70 sm:h-28 sm:w-28"
            />
            <h1 className="mt-5 text-2xl font-semibold tracking-tight text-slate-950">StockControl</h1>
            <p className="mt-1 text-sm text-slate-500">Control de compras e inventario</p>
          </div>

          <form onSubmit={enviar}>
            {error && (
              <div className="mb-4 flex items-start gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2.5 text-sm text-rose-700">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="mt-0.5 h-4 w-4 shrink-0">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
                </svg>
                <span>{error}</span>
              </div>
            )}

            <label className="label">Correo</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoFocus
              autoComplete="email"
              placeholder="tucorreo@empresa.com"
              className="field mb-4 bg-white"
            />

            <label className="label">Contraseña</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="current-password"
              placeholder="••••••••"
              className="field mb-6 bg-white"
            />

            <button
              type="submit"
              disabled={cargando}
              className="w-full rounded-lg bg-[#151b5d] px-4 py-2.5 text-sm font-semibold text-white shadow-sm shadow-slate-950/10 transition hover:bg-[#101545] disabled:cursor-not-allowed disabled:opacity-60"
            >
              {cargando ? 'Ingresando…' : 'Ingresar'}
            </button>
          </form>

          <div className="mt-6 flex items-center justify-center gap-2 text-xs text-slate-400">
            <span>© {new Date().getFullYear()} StockControl</span>
            <span aria-hidden="true">•</span>
            <span>Inventario hotelero</span>
          </div>
        </section>
      </div>
    </main>
  );
}
