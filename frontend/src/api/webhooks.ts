import { api } from './client'

export interface WebhookSubscription {
  id: string
  name: string
  type: 'Slack' | 'Teams' | 'Email' | 'Http'
  target: string
  events: string
  isEnabled: boolean
  createdAt: string
  updatedAt: string | null
}

export interface CreateWebhookRequest {
  name: string
  type: string
  target: string
  events: string
  customHeaders?: string
  secret?: string
}

export interface UpdateWebhookRequest {
  name: string
  target: string
  events: string
  isEnabled: boolean
  customHeaders?: string
  secret?: string
}

export const webhooksApi = {
  list: () => api.get<WebhookSubscription[]>('/webhooks'),
  get: (id: string) => api.get<WebhookSubscription>(`/webhooks/${id}`),
  create: (data: CreateWebhookRequest) => api.post<WebhookSubscription>('/webhooks', data),
  update: (id: string, data: UpdateWebhookRequest) => api.put<WebhookSubscription>(`/webhooks/${id}`, data),
  delete: (id: string) => api.delete(`/webhooks/${id}`),
  test: (id: string) => api.post(`/webhooks/${id}/test`),
}
