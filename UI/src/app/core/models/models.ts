/**
 * TypeScript mirrors of the API DTOs (see build plan Part 3 / Part 4).
 * Kept in one file for easy scanning while the backend is still being built.
 */

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------
export type Role =
  | 'SystemAdministrator'
  | 'DistrictAdministrator'
  | 'PhcAdministrator'
  | 'Doctor'
  | 'PhcStaff';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  districtId: string | null;
  districtName: string | null;
  phcId: string | null;
  phcName: string | null;
}

// ---------------------------------------------------------------------------
// Shared
// ---------------------------------------------------------------------------
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
  search?: string;
}

// ---------------------------------------------------------------------------
// Reference / org data
// ---------------------------------------------------------------------------
export interface District {
  id: string;
  name: string;
  code: string;
  phcCount?: number;
}

export interface Phc {
  id: string;
  districtId: string;
  districtName?: string;
  name: string;
  code: string;
}

export interface Medicine {
  id: string;
  name: string;
  genericName: string;
  unit: string;
  isActive: boolean;
}

export interface Disease {
  id: string;
  name: string;
  code: string;
}

export interface Specialization {
  id: string;
  name: string;
}

export interface UserAccount {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  districtId: string | null;
  phcId: string | null;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Patients / visits
// ---------------------------------------------------------------------------
export interface PrescriptionItemInput {
  medicineId: string;
  quantity: number;
}

export interface RecordVisitRequest {
  localIdentifier: string;
  birthYear: number | null;
  gender: 'M' | 'F' | 'O' | null;
  visitDate: string; // yyyy-MM-dd
  symptoms: string | null;
  attendingDoctorId: string | null;
  diseaseIds: string[];
  prescriptionItems: PrescriptionItemInput[];
}

export interface PatientVisit {
  id: string;
  phcId: string;
  phcName: string;
  localIdentifier: string;
  birthYear: number | null;
  gender: string | null;
  visitDate: string;
  symptoms: string | null;
  attendingDoctorName: string | null;
  diseases: string[];
  medicines: { name: string; quantity: number }[];
}

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------
export type InventoryTransactionType =
  | 'Receipt'
  | 'PatientIssue'
  | 'Adjustment'
  | 'TransferOut'
  | 'TransferIn';

export interface InventoryRow {
  id: string;
  districtId: string;
  phcId: string;
  phcName: string;
  medicineId: string;
  medicineName: string;
  unit: string;
  quantityOnHand: number;
  safetyStock: number;
  expiryDate: string | null;
  isLow: boolean;
}

export interface InventoryTransaction {
  id: string;
  phcId: string;
  phcName: string;
  medicineName: string;
  type: InventoryTransactionType;
  quantity: number;
  balanceAfter: number;
  reference: string | null;
  createdAtUtc: string;
}

export interface StockMutationRequest {
  phcId: string;
  medicineId: string;
  quantity: number; // signed for Adjustment, positive otherwise
  reference: string | null;
  idempotencyKey?: string;
}

// ---------------------------------------------------------------------------
// Medicine requests / transfers
// ---------------------------------------------------------------------------
export type MedicineRequestStatus =
  | 'Pending'
  | 'Accepted'
  | 'Rejected'
  | 'PartiallyFulfilled'
  | 'Fulfilled'
  | 'Completed';

export interface MedicineRequestItem {
  medicineId: string;
  medicineName: string;
  quantityRequested: number;
  quantityFulfilled: number;
}

export interface MedicineRequest {
  id: string;
  sourcePhcId: string;
  sourcePhcName: string;
  destinationPhcId: string;
  destinationPhcName: string;
  status: MedicineRequestStatus;
  notes: string | null;
  neededByDate: string | null;
  requestedByName: string;
  createdAtUtc: string;
  items: MedicineRequestItem[];
}

export interface CreateMedicineRequest {
  destinationPhcId: string;
  notes: string | null;
  neededByDate: string | null;
  items: { medicineId: string; quantityRequested: number }[];
}

export interface FulfillMedicineRequest {
  items: { medicineId: string; quantityFulfilled: number }[];
}

export interface RecommendedSource {
  phcId: string;
  phcName: string;
  availableSurplus: number;
  predictedDemand7d: number;
}

// ---------------------------------------------------------------------------
// Doctors / specialists
// ---------------------------------------------------------------------------
export interface DoctorAvailabilitySlot {
  id?: string;
  dayOfWeek: number; // 0 = Sunday
  startTime: string; // HH:mm
  endTime: string; // HH:mm
}

export interface Doctor {
  id: string;
  phcId: string;
  phcName: string;
  name: string;
  registrationNumber: string | null;
  specializationId: string | null;
  specializationName: string | null;
  availability: DoctorAvailabilitySlot[];
}

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------
export type Severity = 'Info' | 'Warning' | 'Critical';

export interface AppNotification {
  id: string;
  title: string;
  message: string;
  severity: Severity;
  isAcknowledged: boolean;
  createdAtUtc: string;
}

// ---------------------------------------------------------------------------
// Analytics / dashboard
// ---------------------------------------------------------------------------
export interface TimeSeriesPoint {
  date: string;
  value: number;
}

export interface ForecastPoint {
  date: string;
  yhat: number;
  lower: number;
  upper: number;
}

export interface DiseaseTrend {
  diseaseId: string;
  diseaseName: string;
  today: number;
  series: TimeSeriesPoint[];
}

export interface StockoutRisk {
  medicineId: string;
  medicineName: string;
  phcName: string;
  daysToStockout: number | null;
  stockoutDate: string | null;
  risk: Severity;
  recommendedReorderQty: number;
}

export interface AiAlert {
  id: string;
  alertType: 'Stockout' | 'DiseaseAnomaly' | 'PatientVolume';
  message: string;
  severity: Severity;
  isAcknowledged: boolean;
  createdAtUtc: string;
}

export type PredictionStatus = 'Ready' | 'InsufficientData' | 'Stale';

export interface DashboardSummary {
  generatedAtUtc: string;
  predictionStatus: PredictionStatus;
  predictionModelVersion: string | null;
  todayPatientCount: number;
  patientVolumeTrend: TimeSeriesPoint[];
  patientVolumeForecast: ForecastPoint[];
  patientVolumePctChange: number | null;
  diseaseTrends: DiseaseTrend[];
  inventorySummary: { total: number; low: number };
  lowStock: InventoryRow[];
  stockoutRisks: StockoutRisk[];
  aiAlerts: AiAlert[];
  pendingIncomingRequests: number;
  pendingOutgoingRequests: number;
  availableSpecialists: { specialization: string; count: number }[];
}
