import 'dart:async';
import 'dart:convert';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../database/database_helper.dart';
import '../models/flock_batch.dart';

class SyncService {
  static final SyncService instance = SyncService._init();
  SyncService._init();

  String baseUrl = 'http://10.0.2.2:5000/api'; // Standard Android emulator localhost or local IP
  String? authToken;

  final ValueNotifier<bool> isOnline = ValueNotifier<bool>(false);
  final ValueNotifier<bool> isSyncing = ValueNotifier<bool>(false);
  final ValueNotifier<int> pendingCount = ValueNotifier<int>(0);
  final ValueNotifier<String?> lastSyncMessage = ValueNotifier<String?>(null);

  StreamSubscription? _connectivitySubscription;
  Timer? _periodicSyncTimer;

  void initialize({required String apiBaseUrl, String? token}) {
    baseUrl = apiBaseUrl;
    authToken = token;

    // Listen to network status changes
    _connectivitySubscription = Connectivity().onConnectivityChanged.listen((results) {
      final hasConnection = results.any((r) => r != ConnectivityResult.none);
      isOnline.value = hasConnection;
      if (hasConnection) {
        // Target: synchronize within 5 seconds of reconnecting
        Future.delayed(const Duration(seconds: 1), () => syncPendingRecords());
      }
    });

    // Check initial connectivity
    Connectivity().checkConnectivity().then((results) {
      isOnline.value = results.any((r) => r != ConnectivityResult.none);
      refreshPendingCount();
    });

    // Periodic synchronization timer every 30 seconds
    _periodicSyncTimer = Timer.periodic(const Duration(seconds: 30), (_) {
      if (isOnline.value && !isSyncing.value) {
        syncPendingRecords();
      }
    });
  }

  void dispose() {
    _connectivitySubscription?.cancel();
    _periodicSyncTimer?.cancel();
  }

  Future<void> refreshPendingCount() async {
    final count = await DatabaseHelper.instance.getPendingCount();
    pendingCount.value = count;
  }

  /// Push all pending SQLite records to ASP.NET Core server
  Future<void> syncPendingRecords() async {
    if (isSyncing.value) return;

    final pending = await DatabaseHelper.instance.getPendingRecords();
    if (pending.isEmpty) {
      refreshPendingCount();
      return;
    }

    isSyncing.value = true;
    lastSyncMessage.value = 'Syncing ${pending.length} offline records...';

    try {
      final recordsPayload = pending.map((r) => r.toApiPayload()).toList();
      final body = jsonEncode({'records': recordsPayload});

      final response = await http.post(
        Uri.parse('$baseUrl/sync/push'),
        headers: {
          'Content-Type': 'application/json',
          if (authToken != null) 'Authorization': 'Bearer $authToken',
        },
        body: body,
      ).timeout(const Duration(seconds: 8));

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final results = (data['data']['results'] as List?) ?? [];

        int successCount = 0;
        for (var item in results) {
          final clientRecordId = item['clientRecordId'] as String;
          final success = item['success'] as bool;
          final serverVersion = (item['serverVersion'] as int?) ?? 1;
          final errorMessage = item['errorMessage'] as String?;

          if (success) {
            await DatabaseHelper.instance.markRecordSynced(clientRecordId, serverVersion);
            successCount++;
          } else {
            await DatabaseHelper.instance.markRecordFailed(
              clientRecordId,
              errorMessage ?? 'Server validation error',
            );
          }
        }

        lastSyncMessage.value = 'Synchronized $successCount of ${pending.length} records.';
      } else {
        // Server error: preserve local records and mark as failed with error details
        for (var r in pending) {
          await DatabaseHelper.instance.markRecordFailed(
            r.clientRecordId,
            'HTTP ${response.statusCode}: ${response.reasonPhrase}',
          );
        }
        lastSyncMessage.value = 'Sync failed (HTTP ${response.statusCode}). Queued for retry.';
      }
    } catch (e) {
      // Network drop/timeout: preserve records locally
      for (var r in pending) {
        await DatabaseHelper.instance.markRecordFailed(
          r.clientRecordId,
          'Network error: $e',
        );
      }
      lastSyncMessage.value = 'Network unreachable. Records preserved locally.';
    } finally {
      isSyncing.value = false;
      await refreshPendingCount();
    }
  }

  /// Pull active flock batches from server and cache into local SQLite
  Future<void> pullFlockBatches() async {
    if (!isOnline.value) return;

    try {
      final response = await http.get(
        Uri.parse('$baseUrl/farmer/batches'),
        headers: {
          'Content-Type': 'application/json',
          if (authToken != null) 'Authorization': 'Bearer $authToken',
        },
      ).timeout(const Duration(seconds: 8));

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final list = (data['data'] as List?) ?? [];
        final batches = list.map((b) => FlockBatchModel.fromJson(b)).toList();
        await DatabaseHelper.instance.upsertBatches(batches);
      }
    } catch (_) {
      // Silently keep existing cached batches if offline
    }
  }

  /// Alias for pullFlockBatches
  Future<void> pullBatches() => pullFlockBatches();
}
