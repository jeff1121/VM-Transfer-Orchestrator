export interface User {
  id: string
  name: string
  role: string
}

export interface MetricCardData {
  title: string
  value: string | number
  change?: string
  trend?: 'up' | 'down' | 'neutral'
  icon: string
}
