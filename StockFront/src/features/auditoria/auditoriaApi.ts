import { api } from '../../lib/api';

export interface AuditoriaEvento {
  id: number;
  fecha: string;
  usuario: string;
  accion: string;
  entidad: string;
  entidadId: number | null;
  hotelId: number | null;
  hotel: string | null;
  resumen: string;
  detalle: string | null;
}

export interface FiltroAuditoria {
  hotelId?: number;
  accion?: string;
  entidad?: string;
  desde?: string;
  hasta?: string;
}

export const listarAuditoria = (params?: FiltroAuditoria) =>
  api.get<AuditoriaEvento[]>('/api/auditoria', { params }).then((r) => r.data);
