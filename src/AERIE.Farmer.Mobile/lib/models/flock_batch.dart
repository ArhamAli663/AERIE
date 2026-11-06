class FlockBatchModel {
  final String id;
  final String farmName;
  final String houseName;
  final String breedName;
  final String batchCode;
  final DateTime placementDate;
  final int initialChickCount;
  final int currentAliveCount;
  final String status;
  final int ageDays;
  final double? latestFcr;
  final double? mortalityRatePct;
  final double? latestAvgWeightGrams;

  FlockBatchModel({
    required this.id,
    required this.farmName,
    required this.houseName,
    required this.breedName,
    required this.batchCode,
    required this.placementDate,
    required this.initialChickCount,
    required this.currentAliveCount,
    required this.status,
    required this.ageDays,
    this.latestFcr,
    this.mortalityRatePct,
    this.latestAvgWeightGrams,
  });

  factory FlockBatchModel.fromJson(Map<String, dynamic> json) {
    return FlockBatchModel(
      id: json['id'] ?? '',
      farmName: json['farmName'] ?? '',
      houseName: json['houseName'] ?? '',
      breedName: json['breedName'] ?? '',
      batchCode: json['batchCode'] ?? '',
      placementDate: DateTime.parse(json['placementDate']),
      initialChickCount: json['initialChickCount'] ?? 0,
      currentAliveCount: json['currentAliveCount'] ?? 0,
      status: json['status']?.toString() ?? 'Active',
      ageDays: json['ageDays'] ?? 0,
      latestFcr: (json['latestFcr'] as num?)?.toDouble(),
      mortalityRatePct: (json['mortalityRatePct'] as num?)?.toDouble(),
      latestAvgWeightGrams: (json['latestAvgWeightGrams'] as num?)?.toDouble(),
    );
  }

  Map<String, dynamic> toMap() {
    return {
      'id': id,
      'farm_name': farmName,
      'house_name': houseName,
      'breed_name': breedName,
      'batch_code': batchCode,
      'placement_date': placementDate.toIso8601String(),
      'initial_count': initialChickCount,
      'alive_count': currentAliveCount,
      'status': status,
      'age_days': ageDays,
      'latest_fcr': latestFcr,
      'mortality_pct': mortalityRatePct,
      'avg_weight': latestAvgWeightGrams,
    };
  }

  factory FlockBatchModel.fromMap(Map<String, dynamic> map) {
    return FlockBatchModel(
      id: map['id'],
      farmName: map['farm_name'],
      houseName: map['house_name'],
      breedName: map['breed_name'],
      batchCode: map['batch_code'],
      placementDate: DateTime.parse(map['placement_date']),
      initialChickCount: map['initial_count'],
      currentAliveCount: map['alive_count'],
      status: map['status'],
      ageDays: map['age_days'],
      latestFcr: map['latest_fcr'] != null ? (map['latest_fcr'] as num).toDouble() : null,
      mortalityRatePct: map['mortality_pct'] != null ? (map['mortality_pct'] as num).toDouble() : null,
      latestAvgWeightGrams: map['avg_weight'] != null ? (map['avg_weight'] as num).toDouble() : null,
    );
  }
}
