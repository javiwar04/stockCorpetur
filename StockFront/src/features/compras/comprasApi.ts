import { api } from '../../lib/api';
import type { CrearDocumentoCompraRequest, DocumentoCompra, DocumentoCompraResumen } from './types';

export const listarDocumentos = (params?: { hotelId?: number; proveedorId?: number; tipoCompra?: string; desde?: string; hasta?: string }) =>
  api.get<DocumentoCompraResumen[]>('/api/documentos', { params }).then((r) => r.data);

export const obtenerDocumento = (id: number) =>
  api.get<DocumentoCompra>(`/api/documentos/${id}`).then((r) => r.data);

export const crearDocumento = (data: CrearDocumentoCompraRequest) =>
  api.post<DocumentoCompra>('/api/documentos', data).then((r) => r.data);

export const actualizarDocumento = (id: number, data: CrearDocumentoCompraRequest) =>
  api.put<DocumentoCompra>(`/api/documentos/${id}`, data).then((r) => r.data);

export const recibirDocumento = (id: number) =>
  api.patch<DocumentoCompra>(`/api/documentos/${id}/recibir`).then((r) => r.data);

export const anularDocumento = (id: number) =>
  api.patch<DocumentoCompra>(`/api/documentos/${id}/anular`).then((r) => r.data);

export const eliminarDocumento = (id: number) => api.delete(`/api/documentos/${id}`);
