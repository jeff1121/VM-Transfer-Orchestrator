export type JobStatus = 'Created' | 'Queued' | 'Running' | 'Pausing' | 'Paused' | 'Resuming' | 'Cancelling' | 'Cancelled' | 'Failed' | 'Succeeded'
export type StepStatus = 'Pending' | 'Running' | 'Retrying' | 'Failed' | 'Skipped' | 'Succeeded'
export type ConnectionType = 'VSphere' | 'ProxmoxVE' | 'HyperV'
export type PlatformKind = ConnectionType
export type ArtifactFormat = 'Vmdk' | 'Qcow2' | 'Raw' | 'Vhdx'
export type MigrationStrategy = 'FullCopy' | 'Incremental'

export interface Job {
  id: string
  correlationId: string
  strategy: MigrationStrategy
  status: JobStatus
  progress: number
  createdAt: string
  updatedAt: string
  steps: JobStep[]
}

export interface JobStep {
  id: string
  name: string
  order: number
  status: StepStatus
  progress: number
  retryCount: number
  errorMessage?: string
}

export interface JobProgress {
  jobId: string
  status: JobStatus
  overallProgress: number
  steps: JobStep[]
}

export interface Connection {
  id: string
  name: string
  type: ConnectionType
  endpoint: string
  validatedAt?: string
  createdAt: string
}

export interface Artifact {
  id: string
  fileName: string
  format: ArtifactFormat
  checksumAlgorithm: string
  checksumValue: string
  sizeBytes: number
  storageUri: string
  createdAt: string
}

export interface VmInfo {
  id: string
  name: string
  cpuCount: number
  memoryBytes: number
  diskKeys: string[]
}

export interface HyperVVmDiskInfo {
  diskKey: string
  path: string
  sizeBytes: number
  format: string
}

export interface HyperVVmDetails {
  id: string
  name: string
  state: string
  cpuCount: number
  memoryBytes: number
  guestOs: string
  checkpointCount: number
  disks: HyperVVmDiskInfo[]
}

export interface PreFlightCheckItem {
  name: string
  isPassed: boolean
  message: string
  details?: string | null
}

export interface PreFlightCheckResult {
  connectionId: string
  vmId: string
  isAllPassed: boolean
  items: PreFlightCheckItem[]
}

export interface CreateJobRequest {
  sourceConnectionId: string
  targetConnectionId: string
  storageTarget: { type: string; endpoint: string; bucketOrPath: string; region?: string }
  strategy: MigrationStrategy
  options: { targetDiskFormat: ArtifactFormat; verifyChecksum: boolean; maxRetries: number }
  vmId: string
  diskKeys?: string[]
}

export interface CreateConnectionRequest {
  name: string
  type: ConnectionType
  endpoint: string
  secret: string
}
