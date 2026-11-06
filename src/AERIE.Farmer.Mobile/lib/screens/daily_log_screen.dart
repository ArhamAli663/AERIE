import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:uuid/uuid.dart';
import '../database/database_helper.dart';
import '../models/daily_record.dart';
import '../models/flock_batch.dart';
import '../services/sync_service.dart';

class DailyLogScreen extends StatefulWidget {
  final List<FlockBatchModel> batches;
  final String? initialBatchId;

  const DailyLogScreen({
    super.key,
    required this.batches,
    this.initialBatchId,
  });

  @override
  State<DailyLogScreen> createState() => _DailyLogScreenState();
}

class _DailyLogScreenState extends State<DailyLogScreen> {
  final _formKey = GlobalKey<FormState>();
  final _uuid = const Uuid();

  String? _selectedBatchId;
  DateTime _recordDate = DateTime.now();
  final _aliveCountController = TextEditingController();
  final _mortalityController = TextEditingController(text: '0');
  final _feedKgController = TextEditingController();
  final _waterLitersController = TextEditingController();
  final _weightGramsController = TextEditingController();
  final _notesController = TextEditingController();

  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    if (widget.initialBatchId != null &&
        widget.batches.any((b) => b.id == widget.initialBatchId)) {
      _selectedBatchId = widget.initialBatchId;
    } else if (widget.batches.isNotEmpty) {
      _selectedBatchId = widget.batches.first.id;
    }

    _updateAliveCountFromSelectedBatch();
  }

  @override
  void dispose() {
    _aliveCountController.dispose();
    _mortalityController.dispose();
    _feedKgController.dispose();
    _waterLitersController.dispose();
    _weightGramsController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  void _updateAliveCountFromSelectedBatch() {
    if (_selectedBatchId == null) return;
    final batch = widget.batches.firstWhere(
      (b) => b.id == _selectedBatchId,
      orElse: () => widget.batches.first,
    );

    final mortality = int.tryParse(_mortalityController.text) ?? 0;
    final alive = batch.currentAliveCount - mortality;
    _aliveCountController.text = (alive > 0 ? alive : 0).toString();
  }

  void _onMortalityChanged(String val) {
    if (_selectedBatchId == null) return;
    final batch = widget.batches.firstWhere((b) => b.id == _selectedBatchId);
    final mortality = int.tryParse(val) ?? 0;
    final alive = batch.currentAliveCount - mortality;
    _aliveCountController.text = (alive > 0 ? alive : 0).toString();
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _recordDate,
      firstDate: DateTime.now().subtract(const Duration(days: 30)),
      lastDate: DateTime.now(),
    );
    if (picked != null) {
      setState(() => _recordDate = picked);
    }
  }

  Future<void> _saveRecord() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedBatchId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select an active flock batch.')),
      );
      return;
    }

    setState(() => _isSaving = true);

    try {
      final clientRecordId = _uuid.v4();
      final alive = int.parse(_aliveCountController.text);
      final mortality = int.parse(_mortalityController.text);
      final feed = double.parse(_feedKgController.text);
      final water = double.parse(_waterLitersController.text);
      final weight = _weightGramsController.text.isNotEmpty
          ? double.tryParse(_weightGramsController.text)
          : null;
      final notes = _notesController.text.isNotEmpty ? _notesController.text.trim() : null;

      final record = DailyRecordModel(
        clientRecordId: clientRecordId,
        batchId: _selectedBatchId!,
        recordDate: _recordDate,
        aliveCount: alive,
        dailyMortality: mortality,
        feedConsumedKg: feed,
        waterConsumedLiters: water,
        averageWeightGrams: weight,
        notes: notes,
        syncStatus: SyncStatus.pending,
        version: 1,
        createdAt: DateTime.now(),
      );

      // 1. Save directly into local SQLite table
      await DatabaseHelper.instance.insertDailyRecord(record);

      // 2. Refresh pending sync indicator
      await SyncService.instance.refreshPendingCount();

      // 3. If online, trigger auto-sync push in background
      final isOnline = SyncService.instance.isOnline.value;
      if (isOnline) {
        SyncService.instance.syncPendingRecords();
      }

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: const Color(0xFF059669),
            content: Text(
              '✓ Observation saved locally in SQLite.\n'
              '${isOnline ? 'Network detected: auto-syncing with cloud...' : 'Device offline: queued for synchronization.'}',
            ),
            duration: const Duration(seconds: 4),
          ),
        );

        Navigator.of(context).pop(true);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: const Color(0xFFDC2626),
            content: Text('Error saving local record: $e'),
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isSaving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF1E3A8A);
    final dateFormat = DateFormat('EEEE, dd MMM yyyy');

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('Log Daily Observation'),
        backgroundColor: primaryColor,
        foregroundColor: Colors.white,
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Notice banner: Offline resilient
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFEFF6FF),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0xFFBFDBFE)),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.storage_outlined, color: Color(0xFF1D4ED8), size: 20),
                    SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'Offline-First Entry: Observation will be committed to local SQLite and guaranteed idempotently synced to AERIE cloud.',
                        style: TextStyle(
                          fontSize: 12,
                          color: Color(0xFF1E40AF),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Flock Batch Dropdown
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFFE2E8F0)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Target Flock Batch',
                      style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Color(0xFF475569)),
                    ),
                    const SizedBox(height: 8),
                    DropdownButtonFormField<String>(
                      initialValue: _selectedBatchId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      ),
                      items: widget.batches.map((b) {
                        return DropdownMenuItem<String>(
                          value: b.id,
                          child: Text(
                            '${b.batchCode} (${b.farmName} — ${b.houseName}, Day ${b.ageDays})',
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 13),
                          ),
                        );
                      }).toList(),
                      onChanged: (newId) {
                        setState(() {
                          _selectedBatchId = newId;
                          _updateAliveCountFromSelectedBatch();
                        });
                      },
                      validator: (v) => v == null ? 'Please select a batch' : null,
                    ),
                    const SizedBox(height: 16),

                    // Date Picker Row
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'Observation Date',
                              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Color(0xFF475569)),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              dateFormat.format(_recordDate),
                              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
                            ),
                          ],
                        ),
                        OutlinedButton.icon(
                          onPressed: _pickDate,
                          icon: const Icon(Icons.calendar_month, size: 16),
                          label: const Text('Change Date'),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // Production Figures Card
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFFE2E8F0)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Production & Mortality Numbers',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: Color(0xFF0F172A)),
                    ),
                    const SizedBox(height: 14),

                    // Daily Mortality
                    TextFormField(
                      controller: _mortalityController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Daily Mortality (Birds Died Today)',
                        hintText: '0',
                        prefixIcon: Icon(Icons.remove_circle_outline, color: Color(0xFFDC2626)),
                        border: OutlineInputBorder(),
                      ),
                      onChanged: _onMortalityChanged,
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Enter mortality count';
                        final n = int.tryParse(v);
                        if (n == null || n < 0) return 'Must be non-negative integer';
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),

                    // Remaining Alive Count
                    TextFormField(
                      controller: _aliveCountController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Remaining Birds Alive in Shed',
                        prefixIcon: Icon(Icons.check_circle_outline, color: Color(0xFF059669)),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Enter alive count';
                        final n = int.tryParse(v);
                        if (n == null || n < 0) return 'Must be a positive integer';
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),

                    // Feed Consumed
                    TextFormField(
                      controller: _feedKgController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                        labelText: 'Feed Consumed (kg)',
                        hintText: 'e.g. 450.5',
                        prefixIcon: Icon(Icons.grain_outlined),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Enter feed consumed in kg';
                        final n = double.tryParse(v);
                        if (n == null || n < 0) return 'Enter valid kg';
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),

                    // Water Consumed
                    TextFormField(
                      controller: _waterLitersController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                        labelText: 'Water Consumed (Liters)',
                        hintText: 'e.g. 900',
                        prefixIcon: Icon(Icons.water_drop_outlined, color: Color(0xFF0284C7)),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Enter water consumed in liters';
                        final n = double.tryParse(v);
                        if (n == null || n < 0) return 'Enter valid liters';
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),

                    // Average Body Weight (grams)
                    TextFormField(
                      controller: _weightGramsController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                        labelText: 'Sample Average Body Weight (grams) [Optional]',
                        hintText: 'e.g. 1850',
                        prefixIcon: Icon(Icons.scale_outlined),
                        border: OutlineInputBorder(),
                      ),
                      validator: (v) {
                        if (v != null && v.isNotEmpty) {
                          final n = double.tryParse(v);
                          if (n == null || n <= 0) return 'Enter valid weight in grams';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 14),

                    // Notes
                    TextFormField(
                      controller: _notesController,
                      maxLines: 2,
                      decoration: const InputDecoration(
                        labelText: 'Field Notes / Flock Behavior / Medication',
                        hintText: 'e.g. Temperature maintained at 24C, birds active.',
                        prefixIcon: Icon(Icons.notes_outlined),
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              // Save Button
              ElevatedButton.icon(
                onPressed: _isSaving ? null : _saveRecord,
                icon: _isSaving
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Icon(Icons.save),
                label: Text(
                  _isSaving ? 'Writing to SQLite...' : 'Save Record Locally (SQLite)',
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF059669),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  elevation: 0,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
