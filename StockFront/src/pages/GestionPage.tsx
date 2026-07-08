import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  guardarComensal,
  guardarPresupuesto,
  listarComensales,
  listarPresupuestos,
} from '../features/gestion/gestionApi';
import { listarHoteles } from '../features/catalogos/catalogosApi';
import { CATEGORIAS } from '../features/catalogos/types';

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

export function GestionPage() {
  const qc = useQueryClient();
  const hoy = new Date();
  const [anio, setAnio] = useState(hoy.getFullYear());
  const [mes, setMes] = useState(hoy.getMonth() + 1);

  const { data: hoteles } = useQuery({ queryKey: ['hoteles'], queryFn: () => listarHoteles(true) });
  const { data: comensales } = useQuery({
    queryKey: ['comensales', anio, mes],
    queryFn: () => listarComensales(anio, mes),
  });
  const { data: presupuestos } = useQuery({
    queryKey: ['presupuestos', anio, mes],
    queryFn: () => listarPresupuestos(anio, mes),
  });

  // Estado editable local, inicializado desde el servidor.
  const [comensalesEdit, setComensalesEdit] = useState<Record<number, string>>({});
  const [presupuestosEdit, setPresupuestosEdit] = useState<Record<string, string>>({});
  const [guardado, setGuardado] = useState(false);

  useEffect(() => {
    const valores: Record<number, string> = {};
    for (const c of comensales ?? []) valores[c.hotelId] = String(c.numeroComensales);
    setComensalesEdit(valores);
  }, [comensales]);

  useEffect(() => {
    const valores: Record<string, string> = {};
    for (const p of presupuestos ?? []) valores[`${p.hotelId}|${p.categoria}`] = String(p.monto);
    setPresupuestosEdit(valores);
  }, [presupuestos]);

  const guardarMutation = useMutation({
    mutationFn: async () => {
      const tareas: Promise<unknown>[] = [];

      for (const [hotelId, valor] of Object.entries(comensalesEdit)) {
        if (valor === '') continue;
        tareas.push(guardarComensal({ hotelId: Number(hotelId), anio, mes, numeroComensales: Number(valor) }));
      }
      for (const [clave, valor] of Object.entries(presupuestosEdit)) {
        if (valor === '') continue;
        const [hotelId, categoria] = clave.split('|');
        tareas.push(guardarPresupuesto({ hotelId: Number(hotelId), categoria, anio, mes, monto: Number(valor) }));
      }
      await Promise.all(tareas);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['comensales'] });
      qc.invalidateQueries({ queryKey: ['presupuestos'] });
      qc.invalidateQueries({ queryKey: ['dash-resumen'] });
      setGuardado(true);
      setTimeout(() => setGuardado(false), 2500);
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="page-title">Gestión mensual</h1>
          <p className="page-subtitle">Comensales y presupuestos por hotel — alimentan el food cost del dashboard.</p>
        </div>
        <div className="flex items-center gap-2">
          <select value={mes} onChange={(e) => setMes(Number(e.target.value))} className="field w-auto">
            {MESES.map((m, i) => (
              <option key={m} value={i + 1}>
                {m}
              </option>
            ))}
          </select>
          <input type="number" value={anio} onChange={(e) => setAnio(Number(e.target.value))} className="field w-24" />
        </div>
      </div>

      <div className="card card-pad">
        <h2 className="card-title mb-4">Comensales del mes</h2>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
          {hoteles?.map((h) => (
            <div key={h.id}>
              <label className="label">{h.nombre}</label>
              <input
                type="number"
                min="0"
                placeholder="0"
                value={comensalesEdit[h.id] ?? ''}
                onChange={(e) => setComensalesEdit((prev) => ({ ...prev, [h.id]: e.target.value }))}
                className="field"
              />
            </div>
          ))}
        </div>
      </div>

      <div className="card card-pad overflow-x-auto">
        <h2 className="card-title mb-4">Presupuesto del mes (Q por categoría)</h2>
        <table className="w-full min-w-[640px] text-sm">
          <thead className="thead">
            <tr>
              <th className="px-2 py-2.5">Hotel</th>
              {CATEGORIAS.map((c) => (
                <th key={c} className="px-2 py-2.5 text-center">
                  {c}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {hoteles?.map((h) => (
              <tr key={h.id} className="border-t border-slate-100">
                <td className="px-2 py-2 font-medium text-slate-700">{h.nombre}</td>
                {CATEGORIAS.map((c) => {
                  const clave = `${h.id}|${c}`;
                  return (
                    <td key={c} className="px-1 py-1.5">
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        placeholder="—"
                        value={presupuestosEdit[clave] ?? ''}
                        onChange={(e) => setPresupuestosEdit((prev) => ({ ...prev, [clave]: e.target.value }))}
                        className="field px-2 py-1.5 text-right"
                      />
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center gap-3">
        <button onClick={() => guardarMutation.mutate()} disabled={guardarMutation.isPending} className="btn-primary">
          {guardarMutation.isPending ? 'Guardando…' : 'Guardar cambios del mes'}
        </button>
        {guardado && <span className="text-sm font-medium text-emerald-600">✓ Guardado</span>}
        {guardarMutation.isError && <span className="text-sm text-rose-600">Error al guardar. Intenta de nuevo.</span>}
      </div>
    </div>
  );
}
