import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  agregarConversion,
  listarConversiones,
  listarUnidades,
} from '../features/catalogos/catalogosApi';

interface Props {
  productoId: number;
  unidadBaseNombre: string;
  puedeEditar: boolean;
}

/**
 * Conversiones de un producto: cuántas unidades base equivale cada unidad de
 * compra (1 caja = 25 libras). Sin esto, comprar en cajas no compara contra
 * comprar en libras.
 */
export function ConversionesPanel({ productoId, unidadBaseNombre, puedeEditar }: Props) {
  const qc = useQueryClient();
  const { data: conversiones, isLoading } = useQuery({
    queryKey: ['conversiones', productoId],
    queryFn: () => listarConversiones(productoId),
  });
  const { data: unidades } = useQuery({ queryKey: ['unidades'], queryFn: listarUnidades });

  const [unidadId, setUnidadId] = useState<number | ''>('');
  const [factor, setFactor] = useState('');
  const [error, setError] = useState<string | null>(null);

  const agregarMutation = useMutation({
    mutationFn: () => agregarConversion(productoId, { unidadId: Number(unidadId), factorABase: Number(factor) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['conversiones', productoId] });
      setUnidadId('');
      setFactor('');
      setError(null);
    },
    onError: (e: unknown) => {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'No se pudo agregar la conversión.');
    },
  });

  // Unidades que aún no tienen conversión para este producto.
  const disponibles = unidades?.filter((u) => !conversiones?.some((c) => c.unidadId === u.id));

  return (
    <div className="rounded-xl bg-slate-50 p-4 text-sm ring-1 ring-slate-200/70">
      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Unidades de compra</div>
      {isLoading && <p className="text-slate-400">Cargando…</p>}
      <ul className="mb-3 space-y-1">
        {conversiones?.map((c) => (
          <li key={c.id} className="text-slate-600">
            1 {c.unidadNombre} = <b className="text-slate-800">{c.factorABase}</b> {unidadBaseNombre}
            {c.factorABase === 1 && c.unidadNombre === unidadBaseNombre && (
              <span className="ml-1 text-xs text-slate-400">(unidad base)</span>
            )}
          </li>
        ))}
      </ul>

      {puedeEditar && (
        <div className="flex flex-wrap items-center gap-2">
          <select
            value={unidadId}
            onChange={(e) => setUnidadId(e.target.value === '' ? '' : Number(e.target.value))}
            className="field w-auto"
          >
            <option value="">Agregar unidad…</option>
            {disponibles?.map((u) => (
              <option key={u.id} value={u.id}>
                {u.nombre}
              </option>
            ))}
          </select>
          <span className="text-slate-400">=</span>
          <input
            type="number"
            step="0.0001"
            min="0"
            placeholder="factor"
            value={factor}
            onChange={(e) => setFactor(e.target.value)}
            className="field w-24"
          />
          <span className="text-slate-500">{unidadBaseNombre}</span>
          <button
            onClick={() => agregarMutation.mutate()}
            disabled={!unidadId || !factor || Number(factor) <= 0 || agregarMutation.isPending}
            className="btn-primary btn-sm"
          >
            Agregar
          </button>
          {error && <span className="text-xs text-rose-600">{error}</span>}
        </div>
      )}
    </div>
  );
}
