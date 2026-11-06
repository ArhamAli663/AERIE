import 'package:flutter/material.dart';
import '../database/database_helper.dart';
import '../models/flock_batch.dart';
import '../services/sync_service.dart';
import 'daily_log_screen.dart';
import 'login_screen.dart';
import 'sync_history_screen.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  List<FlockBatchModel> _batches = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _loadBatches();
    SyncService.instance.refreshPendingCount();
  }

  Future<void> _loadBatches() async {
    setState(() => _isLoading = true);
    final localBatches = await DatabaseHelper.instance.getBatches();
    setState(() {
      _batches = localBatches;
      _isLoading = false;
    });
  }

  Future<void> _handleRefresh() async {
    if (SyncService.instance.isOnline.value) {
      await SyncService.instance.syncPendingRecords();
      await SyncService.instance.pullBatches();
    }
    await _loadBatches();
    await SyncService.instance.refreshPendingCount();
  }

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF1E3A8A);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'AERIE Poultry Farm',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
            ),
            Text(
              'Flock Production Manager',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.normal, color: Color(0xFFCBD5E1)),
            ),
          ],
        ),
        backgroundColor: primaryColor,
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Synchronize & Refresh',
            onPressed: _handleRefresh,
          ),
          ValueListenableBuilder<int>(
            valueListenable: SyncService.instance.pendingCount,
            builder: (context, count, _) {
              return Stack(
                alignment: Alignment.center,
                children: [
                  IconButton(
                    icon: const Icon(Icons.sync_alt),
                    tooltip: 'Sync Queue & Audit',
                    onPressed: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => const SyncHistoryScreen()),
                      ).then((_) => _loadBatches());
                    },
                  ),
                  if (count > 0)
                    Positioned(
                      top: 8,
                      right: 8,
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: const BoxDecoration(
                          color: Color(0xFFF59E0B),
                          shape: BoxShape.circle,
                        ),
                        constraints: const BoxConstraints(minWidth: 16, minHeight: 16),
                        child: Text(
                          '$count',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign Out',
            onPressed: () {
              Navigator.of(context).pushReplacement(
                MaterialPageRoute(builder: (_) => const LoginScreen()),
              );
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _handleRefresh,
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Connectivity Status Banner
              _buildConnectivityBanner(),
              const SizedBox(height: 12),

              // Pending Sync Alert Bar
              _buildPendingSyncBar(),
              const SizedBox(height: 16),

              // Summary Stats Row
              _buildSummaryKpiCards(),
              const SizedBox(height: 20),

              // Section Header
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Active Flock Batches',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  Text(
                    '${_batches.length} Sheds Cached',
                    style: const TextStyle(
                      fontSize: 12,
                      color: Color(0xFF64748B),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),

              // Batches List
              if (_isLoading)
                const Center(
                  child: Padding(
                    padding: EdgeInsets.all(32.0),
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (_batches.isEmpty)
                _buildEmptyBatchesCard()
              else
                ..._batches.map((batch) => _buildBatchCard(batch)),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final result = await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => DailyLogScreen(batches: _batches),
            ),
          );
          if (result == true) {
            _loadBatches();
          }
        },
        backgroundColor: const Color(0xFF059669),
        foregroundColor: Colors.white,
        icon: const Icon(Icons.add_task),
        label: const Text(
          'Log Daily Observation',
          style: TextStyle(fontWeight: FontWeight.w600),
        ),
      ),
    );
  }

  Widget _buildConnectivityBanner() {
    return ValueListenableBuilder<bool>(
      valueListenable: SyncService.instance.isOnline,
      builder: (context, online, _) {
        return ValueListenableBuilder<bool>(
          valueListenable: SyncService.instance.isSyncing,
          builder: (context, syncing, _) {
            final color = syncing
                ? const Color(0xFF2563EB)
                : (online ? const Color(0xFF059669) : const Color(0xFFD97706));
            final icon = syncing
                ? Icons.sync
                : (online ? Icons.wifi : Icons.wifi_off);
            final label = syncing
                ? 'Cloud Syncing In Progress...'
                : (online ? 'Online — Connected to AERIE Cloud Gateway' : 'Offline Mode — Operating on Local SQLite Storage');

            return Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: color.withValues(alpha: 0.3)),
              ),
              child: Row(
                children: [
                  Icon(icon, size: 16, color: color),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      label,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: color,
                      ),
                    ),
                  ),
                ],
              ),
            );
          },
        );
      },
    );
  }

  Widget _buildPendingSyncBar() {
    return ValueListenableBuilder<int>(
      valueListenable: SyncService.instance.pendingCount,
      builder: (context, count, _) {
        if (count == 0) return const SizedBox.shrink();

        return Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: const Color(0xFFFFFBEB),
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: const Color(0xFFFDE68A)),
          ),
          child: Row(
            children: [
              const Icon(Icons.schedule_send, color: Color(0xFFD97706), size: 20),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '$count observation record(s) queued for sync.',
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFF92400E),
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
              ElevatedButton(
                onPressed: () => SyncService.instance.syncPendingRecords(),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFD97706),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  minimumSize: Size.zero,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  elevation: 0,
                ),
                child: const Text('Sync Now', style: TextStyle(fontSize: 12)),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildSummaryKpiCards() {
    final totalBirds = _batches.fold<int>(0, (sum, b) => sum + b.currentAliveCount);
    final totalPlaced = _batches.fold<int>(0, (sum, b) => sum + b.initialChickCount);
    final overallMortality = totalPlaced > 0
        ? (((totalPlaced - totalBirds) / totalPlaced) * 100).toStringAsFixed(1)
        : '0.0';

    return Row(
      children: [
        Expanded(
          child: _buildMetricMiniCard(
            label: 'Active Birds',
            value: totalBirds.toString(),
            subtext: '${_batches.length} Sheds',
            icon: Icons.egg_outlined,
            color: const Color(0xFF1E3A8A),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _buildMetricMiniCard(
            label: 'Initial Placed',
            value: totalPlaced.toString(),
            subtext: 'Cumulative',
            icon: Icons.calendar_today_outlined,
            color: const Color(0xFF0D9488),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _buildMetricMiniCard(
            label: 'Mortality %',
            value: '$overallMortality%',
            subtext: 'Overall',
            icon: Icons.show_chart_outlined,
            color: const Color(0xFFE11D48),
          ),
        ),
      ],
    );
  }

  Widget _buildMetricMiniCard({
    required String label,
    required String value,
    required String subtext,
    required IconData icon,
    required Color color,
  }) {
    return Container(
      padding: const EdgeInsets.all(12),
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
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                label,
                style: const TextStyle(fontSize: 11, color: Color(0xFF64748B), fontWeight: FontWeight.w500),
              ),
              Icon(icon, size: 14, color: color),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            subtext,
            style: const TextStyle(fontSize: 10, color: Color(0xFF94A3B8)),
          ),
        ],
      ),
    );
  }

  Widget _buildEmptyBatchesCard() {
    return Container(
      padding: const EdgeInsets.all(32),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        children: [
          const Icon(Icons.inbox_outlined, size: 48, color: Color(0xFF94A3B8)),
          const SizedBox(height: 12),
          const Text(
            'No Flock Batches Cached',
            style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: Color(0xFF334155)),
          ),
          const SizedBox(height: 6),
          const Text(
            'Connect to the network and pull flock batches from the AERIE cloud server, or wait for sync.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: _handleRefresh,
            icon: const Icon(Icons.cloud_download_outlined, size: 16),
            label: const Text('Pull Batches From Cloud'),
          ),
        ],
      ),
    );
  }

  Widget _buildBatchCard(FlockBatchModel batch) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
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
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () async {
          final result = await Navigator.of(context).push(
            MaterialPageRoute(
              builder: (_) => DailyLogScreen(batches: _batches, initialBatchId: batch.id),
            ),
          );
          if (result == true) {
            _loadBatches();
          }
        },
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header: Batch Code & Status
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                        decoration: BoxDecoration(
                          color: const Color(0xFFEFF6FF),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(color: const Color(0xFFBFDBFE)),
                        ),
                        child: Text(
                          batch.batchCode,
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF1D4ED8),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Day ${batch.ageDays}',
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF475569),
                        ),
                      ),
                    ],
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: batch.status == 'Active'
                          ? const Color(0xFFECFDF5)
                          : const Color(0xFFF1F5F9),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      batch.status,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: batch.status == 'Active'
                            ? const Color(0xFF059669)
                            : const Color(0xFF64748B),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),

              // Farm & House details
              Text(
                '${batch.farmName} — ${batch.houseName}',
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF0F172A),
                ),
              ),
              Text(
                'Breed: ${batch.breedName}',
                style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
              ),
              const Divider(height: 20, color: Color(0xFFF1F5F9)),

              // Metrics row
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  _buildStatColumn('Alive Birds', '${batch.currentAliveCount} / ${batch.initialChickCount}'),
                  _buildStatColumn('FCR', batch.latestFcr != null ? batch.latestFcr!.toStringAsFixed(2) : '—'),
                  _buildStatColumn('Avg Wt', batch.latestAvgWeightGrams != null ? '${batch.latestAvgWeightGrams!.toStringAsFixed(0)}g' : '—'),
                  _buildStatColumn('Mortality', batch.mortalityRatePct != null ? '${batch.mortalityRatePct!.toStringAsFixed(1)}%' : '—'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStatColumn(String label, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 10, color: Color(0xFF94A3B8), fontWeight: FontWeight.w500),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Color(0xFF334155)),
        ),
      ],
    );
  }
}
