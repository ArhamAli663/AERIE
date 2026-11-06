enum SyncStatus { pending, syncing, synced, failed }

class DailyRecordModel {
  final String clientRecordId;
  final String batchId;
  final DateTime recordDate;
  final int aliveCount;
  final int dailyMortality;
  final double feedConsumedKg;
  final double waterConsumedLiters;
  final double? averageWeightGrams;
  final String? notes;
  SyncStatus syncStatus;
  int version;
  int retryCount;
  String? lastError;
  final DateTime createdAt;

  DailyRecordModel({
    required this.clientRecordId,
    required this.batchId,
    required this.recordDate,
    required this.aliveCount,
    required this.dailyMortality,
    required this.feedConsumedKg,
    required this.waterConsumedLiters,
    this.averageWeightGrams,
    this.notes,
    this.syncStatus = SyncStatus.pending,
    this.version = 1,
    this.retryCount = 0,
    this.lastError,
    DateTime? createdAt,
  }) : createdAt = createdAt ?? DateTime.now();

  Map<String, dynamic> toMap() {
    return {
      'client_record_id': clientRecordId,
      'batch_id': batchId,
      'record_date': recordDate.toIso8601String().split('T')[0],
      'alive_count': aliveCount,
      'daily_mortality': dailyMortality,
      'feed_consumed_kg': feedConsumedKg,
      'water_consumed_liters': waterConsumedLiters,
      'avg_weight_grams': averageWeightGrams,
      'notes': notes,
      'sync_status': syncStatus.name,
      'version': version,
      'retry_count': retryCount,
      'last_error': lastError,
      'created_at': createdAt.toIso8601String(),
    };
  }

  factory DailyRecordModel.fromMap(Map<String, dynamic> map) {
    return DailyRecordModel(
      clientRecordId: map['client_record_id'],
      batchId: map['batch_id'],
      recordDate: DateTime.parse(map['record_date']),
      aliveCount: map['alive_count'],
      dailyMortality: map['daily_mortality'],
      feedConsumedKg: (map['feed_consumed_kg'] as num).toDouble(),
      waterConsumedLiters: (map['water_consumed_liters'] as num).toDouble(),
      averageWeightGrams: map['avg_weight_grams'] != null
          ? (map['avg_weight_grams'] as num).toDouble()
          : null,
      notes: map['notes'],
      syncStatus: SyncStatus.values.firstWhere(
        (e) => e.name == map['sync_status'],
        orElse: () => SyncStatus.pending,
      ),
      version: map['version'] ?? 1,
      retryCount: map['retry_count'] ?? 0,
      lastError: map['last_error'],
      createdAt: DateTime.parse(map['created_at']),
    );
  }

  Map<String, dynamic> toApiPayload() {
    return {
      'clientRecordId': clientRecordId,
      'batchId': batchId,
      'recordDate': recordDate.toIso8601String().split('T')[0],
      'aliveCount': aliveCount,
      'dailyMortality': dailyMortality,
      'feedConsumedKg': feedConsumedKg,
      'waterConsumedLiters': waterConsumedLiters,
      'averageWeightGrams': averageWeightGrams,
      'notes': notes,
      'clientVersion': version,
      'clientTimestamp': createdAt.toIso8601String(),
    };
  }
}
