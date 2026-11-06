import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';
import '../models/daily_record.dart';
import '../models/flock_batch.dart';

class DatabaseHelper {
  static final DatabaseHelper instance = DatabaseHelper._init();
  static Database? _database;

  DatabaseHelper._init();

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDB('aerie_farmer_offline.db');
    return _database!;
  }

  Future<Database> _initDB(String filePath) async {
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, filePath);

    return await openDatabase(
      path,
      version: 1,
      onCreate: _createDB,
    );
  }

  Future _createDB(Database db, int version) async {
    // Local cache of active flock batches
    await db.execute('''
      CREATE TABLE flock_batches (
        id TEXT PRIMARY KEY,
        farm_name TEXT NOT NULL,
        house_name TEXT NOT NULL,
        breed_name TEXT NOT NULL,
        batch_code TEXT NOT NULL,
        placement_date TEXT NOT NULL,
        initial_count INTEGER NOT NULL,
        alive_count INTEGER NOT NULL,
        status TEXT NOT NULL,
        age_days INTEGER NOT NULL,
        latest_fcr REAL,
        mortality_pct REAL,
        avg_weight REAL
      )
    ''');

    // Offline daily observation logs queue
    await db.execute('''
      CREATE TABLE offline_daily_records (
        client_record_id TEXT PRIMARY KEY,
        batch_id TEXT NOT NULL,
        record_date TEXT NOT NULL,
        alive_count INTEGER NOT NULL,
        daily_mortality INTEGER NOT NULL,
        feed_consumed_kg REAL NOT NULL,
        water_consumed_liters REAL NOT NULL,
        avg_weight_grams REAL,
        notes TEXT,
        sync_status TEXT NOT NULL,
        version INTEGER NOT NULL,
        retry_count INTEGER NOT NULL,
        last_error TEXT,
        created_at TEXT NOT NULL
      )
    ''');
  }

  // ── Daily Records Offline CRUD ──────────────────────────────────────────────

  Future<void> insertDailyRecord(DailyRecordModel record) async {
    final db = await instance.database;
    await db.insert(
      'offline_daily_records',
      record.toMap(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<DailyRecordModel>> getPendingRecords() async {
    final db = await instance.database;
    final maps = await db.query(
      'offline_daily_records',
      where: 'sync_status = ? OR sync_status = ?',
      whereArgs: [SyncStatus.pending.name, SyncStatus.failed.name],
      orderBy: 'created_at ASC',
    );
    return maps.map((m) => DailyRecordModel.fromMap(m)).toList();
  }

  Future<void> updateSyncStatus(String clientRecordId, SyncStatus status, {String? error}) async {
    final db = await instance.database;
    await db.update(
      'offline_daily_records',
      {
        'sync_status': status.name,
        'last_error': error,
      },
      where: 'client_record_id = ?',
      whereArgs: [clientRecordId],
    );
  }

  Future<void> markRecordSynced(String clientRecordId, int serverVersion) async {
    final db = await instance.database;
    await db.update(
      'offline_daily_records',
      {
        'sync_status': SyncStatus.synced.name,
        'version': serverVersion,
        'last_error': null,
      },
      where: 'client_record_id = ?',
      whereArgs: [clientRecordId],
    );
  }

  Future<void> markRecordFailed(String clientRecordId, String errorMessage) async {
    final db = await instance.database;
    final record = await db.query(
      'offline_daily_records',
      columns: ['retry_count'],
      where: 'client_record_id = ?',
      whereArgs: [clientRecordId],
    );
    final currentRetries = record.isNotEmpty ? (record.first['retry_count'] as int) : 0;

    await db.update(
      'offline_daily_records',
      {
        'sync_status': SyncStatus.failed.name,
        'retry_count': currentRetries + 1,
        'last_error': errorMessage,
      },
      where: 'client_record_id = ?',
      whereArgs: [clientRecordId],
    );
  }

  Future<List<DailyRecordModel>> getAllDailyRecords() async {
    final db = await instance.database;
    final maps = await db.query('offline_daily_records', orderBy: 'created_at DESC');
    return maps.map((m) => DailyRecordModel.fromMap(m)).toList();
  }

  Future<int> getPendingCount() async {
    final db = await instance.database;
    final res = await db.rawQuery('''
      SELECT COUNT(*) as count FROM offline_daily_records 
      WHERE sync_status = ? OR sync_status = ?
    ''', [SyncStatus.pending.name, SyncStatus.failed.name]);
    return Sqflite.firstIntValue(res) ?? 0;
  }

  // ── Flock Batches Offline Cache ─────────────────────────────────────────────

  Future<void> upsertBatches(List<FlockBatchModel> batches) async {
    final db = await instance.database;
    final batch = db.batch();
    for (var b in batches) {
      batch.insert('flock_batches', b.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
    }
    await batch.commit(noResult: true);
  }

  Future<List<FlockBatchModel>> getBatches() async {
    final db = await instance.database;
    final maps = await db.query('flock_batches', orderBy: 'placement_date DESC');
    return maps.map((m) => FlockBatchModel.fromMap(m)).toList();
  }
}
