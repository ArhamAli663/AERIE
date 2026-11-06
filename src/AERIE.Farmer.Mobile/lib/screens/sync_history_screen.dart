import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../database/database_helper.dart';
import '../models/daily_record.dart';
import '../services/sync_service.dart';

class SyncHistoryScreen extends StatefulWidget {
  const SyncHistoryScreen({super.key});

  @override
  State<SyncHistoryScreen> createState() => _SyncHistoryScreenState();
}

class _SyncHistoryScreenState extends State<SyncHistoryScreen> {
  List<DailyRecordModel> _records = [];
  bool _isLoading = false;
  String _selectedFilter = 'all';

  @override
  void initState() {
    super.initState();
    _loadRecords();
  }

  Future<void> _loadRecords() async {
    setState(() => _isLoading = true);
    final all = await DatabaseHelper.instance.getAllDailyRecords();
    setState(() {
      _records = all;
      _isLoading = false;
    });
    await SyncService.instance.refreshPendingCount();
  }

  Future<void> _triggerManualSync() async {
    if (!SyncService.instance.isOnline.value) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Color(0xFFD97706),
          content: Text('Device is offline. Connect to Wi-Fi or mobile data to sync.'),
        ),
      );
      return;
    }

    await SyncService.instance.syncPendingRecords();
    await _loadRecords();
  }

  List<DailyRecordModel> get _filteredRecords {
    if (_selectedFilter == 'pending') {
      return _records.where((r) => r.syncStatus == SyncStatus.pending).toList();
    }
    if (_selectedFilter == 'synced') {
      return _records.where((r) => r.syncStatus == SyncStatus.synced).toList();
    }
    if (_selectedFilter == 'failed') {
      return _records.where((r) => r.syncStatus == SyncStatus.failed).toList();
    }
    return _records;
  }

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF1E3A8A);
    final dateFormat = DateFormat('dd MMM yyyy, HH:mm');

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('Offline Queue & Sync History'),
        backgroundColor: primaryColor,
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.sync),
            tooltip: 'Trigger Sync Push',
            onPressed: _triggerManualSync,
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadRecords,
        child: Column(
          children: [
            // Status bar
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              color: Colors.white,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  ValueListenableBuilder<bool>(
                    valueListenable: SyncService.instance.isOnline,
                    builder: (context, online, _) {
                      return Row(
                        children: [
                          Icon(
                            online ? Icons.cloud_done : Icons.cloud_off,
                            color: online ? const Color(0xFF059669) : const Color(0xFFD97706),
                            size: 18,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            online ? 'Connected to Cloud' : 'Offline Mode',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w600,
                              color: online ? const Color(0xFF059669) : const Color(0xFFD97706),
                            ),
                          ),
                        ],
                      );
                    },
                  ),
                  ValueListenableBuilder<int>(
                    valueListenable: SyncService.instance.pendingCount,
                    builder: (context, count, _) {
                      return Text(
                        '$count Pending Sync',
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF475569),
                        ),
                      );
                    },
                  ),
                ],
              ),
            ),
            const Divider(height: 1, color: Color(0xFFE2E8F0)),

            // Filter chips
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              color: Colors.white,
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    _buildFilterChip('all', 'All (${_records.length})'),
                    const SizedBox(width: 8),
                    _buildFilterChip(
                      'pending',
                      'Pending (${_records.where((r) => r.syncStatus == SyncStatus.pending).length})',
                    ),
                    const SizedBox(width: 8),
                    _buildFilterChip(
                      'synced',
                      'Synced (${_records.where((r) => r.syncStatus == SyncStatus.synced).length})',
                    ),
                    const SizedBox(width: 8),
                    _buildFilterChip(
                      'failed',
                      'Failed (${_records.where((r) => r.syncStatus == SyncStatus.failed).length})',
                    ),
                  ],
                ),
              ),
            ),
            const Divider(height: 1, color: Color(0xFFE2E8F0)),

            // Records List
            Expanded(
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : _filteredRecords.isEmpty
                      ? _buildEmptyState()
                      : ListView.builder(
                          padding: const EdgeInsets.all(16),
                          itemCount: _filteredRecords.length,
                          itemBuilder: (context, index) {
                            final record = _filteredRecords[index];
                            return _buildRecordCard(record, dateFormat);
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFilterChip(String key, String label) {
    final isSelected = _selectedFilter == key;
    return ChoiceChip(
      label: Text(label, style: TextStyle(fontSize: 12, color: isSelected ? Colors.white : const Color(0xFF334155))),
      selected: isSelected,
      selectedColor: const Color(0xFF1E3A8A),
      backgroundColor: const Color(0xFFF1F5F9),
      onSelected: (_) => setState(() => _selectedFilter = key),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.playlist_add_check_circle_outlined, size: 56, color: Color(0xFF94A3B8)),
            const SizedBox(height: 12),
            const Text(
              'No Records Found',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: Color(0xFF334155)),
            ),
            const SizedBox(height: 6),
            const Text(
              'Daily observations recorded from the shed will be logged here with their synchronization status.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildRecordCard(DailyRecordModel record, DateFormat dateFormat) {
    Color badgeColor;
    Color badgeBg;
    String statusText;
    IconData statusIcon;

    switch (record.syncStatus) {
      case SyncStatus.synced:
        badgeColor = const Color(0xFF059669);
        badgeBg = const Color(0xFFECFDF5);
        statusText = 'Synced (v${record.version})';
        statusIcon = Icons.check_circle;
        break;
      case SyncStatus.syncing:
        badgeColor = const Color(0xFF2563EB);
        badgeBg = const Color(0xFFEFF6FF);
        statusText = 'Syncing...';
        statusIcon = Icons.sync;
        break;
      case SyncStatus.failed:
        badgeColor = const Color(0xFFDC2626);
        badgeBg = const Color(0xFFFEF2F2);
        statusText = 'Failed (${record.retryCount} retries)';
        statusIcon = Icons.warning_amber_rounded;
        break;
      case SyncStatus.pending:
        badgeColor = const Color(0xFFD97706);
        badgeBg = const Color(0xFFFFFBEB);
        statusText = 'Pending Sync';
        statusIcon = Icons.schedule;
        break;
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE2E8F0)),
        boxShadow: const [
          BoxShadow(
            color: Color(0x05000000),
            blurRadius: 4,
            offset: Offset(0, 2),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Top Row: Date & Status Badge
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                DateFormat('yyyy-MM-dd').format(record.recordDate),
                style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Color(0xFF0F172A)),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: badgeBg,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: badgeColor.withValues(alpha: 0.3)),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(statusIcon, size: 12, color: badgeColor),
                    const SizedBox(width: 4),
                    Text(
                      statusText,
                      style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: badgeColor),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),

          // Details grid
          Row(
            children: [
              _buildDetailItem('Alive Birds', '${record.aliveCount}'),
              _buildDetailItem('Mortality', '${record.dailyMortality}', isDanger: record.dailyMortality > 0),
              _buildDetailItem('Feed Consumed', '${record.feedConsumedKg} kg'),
              _buildDetailItem('Water', '${record.waterConsumedLiters} L'),
            ],
          ),
          if (record.averageWeightGrams != null) ...[
            const SizedBox(height: 6),
            Text(
              'Sample Weight: ${record.averageWeightGrams!.toStringAsFixed(0)} g',
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: Color(0xFF334155)),
            ),
          ],
          if (record.notes != null && record.notes!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              'Notes: ${record.notes}',
              style: const TextStyle(fontSize: 12, fontStyle: FontStyle.italic, color: Color(0xFF64748B)),
            ),
          ],

          if (record.lastError != null) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFFFEF2F2),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                'Sync Error: ${record.lastError}',
                style: const TextStyle(fontSize: 11, color: Color(0xFF991B1B)),
              ),
            ),
          ],

          const Divider(height: 16, color: Color(0xFFF1F5F9)),

          // Footer: Client Record Id and Timestamp
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'UUID: ${record.clientRecordId.substring(0, 13)}...',
                style: const TextStyle(fontSize: 10, color: Color(0xFF94A3B8), fontFamily: 'monospace'),
              ),
              Text(
                dateFormat.format(record.createdAt),
                style: const TextStyle(fontSize: 10, color: Color(0xFF94A3B8)),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDetailItem(String label, String value, {bool isDanger = false}) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 10, color: Color(0xFF94A3B8), fontWeight: FontWeight.w500),
          ),
          const SizedBox(height: 2),
          Text(
            value,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: isDanger ? const Color(0xFFDC2626) : const Color(0xFF1E293B),
            ),
          ),
        ],
      ),
    );
  }
}
