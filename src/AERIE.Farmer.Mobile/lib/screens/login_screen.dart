import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../services/sync_service.dart';
import 'dashboard_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _serverUrlController = TextEditingController(text: 'http://10.0.2.2:5000/api');
  final _emailController = TextEditingController(text: 'farmer@albaraka.com');
  final _passwordController = TextEditingController(text: 'Farmer@123456');

  bool _isLoading = false;
  String? _errorMessage;

  @override
  void dispose() {
    _serverUrlController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _prefillFarmerCredentials() {
    setState(() {
      _emailController.text = 'farmer@albaraka.com';
      _passwordController.text = 'Farmer@123456';
    });
  }

  Future<void> _handleLogin() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    final baseUrl = _serverUrlController.text.trim();
    final email = _emailController.text.trim();
    final password = _passwordController.text;

    try {
      final response = await http.post(
        Uri.parse('$baseUrl/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'email': email,
          'password': password,
        }),
      ).timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final token = data['token'] as String;

        // Initialize sync service with host and bearer token
        SyncService.instance.initialize(
          apiBaseUrl: baseUrl,
          token: token,
        );

        // Prime local SQLite database with flock batches from cloud
        await SyncService.instance.pullBatches();

        if (mounted) {
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(
              builder: (ctx) => const DashboardScreen(),
            ),
          );
        }
      } else {
        setState(() {
          _errorMessage = 'Authentication failed (${response.statusCode}): Invalid email or password.';
        });
      }
    } catch (e) {
      // Offline fallback: allow farmer to enter in offline field mode if local database exists
      setState(() {
        _errorMessage = 'Could not reach server: $e\n\nTip: You can continue offline to log records locally if batches were previously synchronized.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _enterOfflineMode() {
    SyncService.instance.initialize(
      apiBaseUrl: _serverUrlController.text.trim(),
      token: null,
    );

    Navigator.of(context).pushReplacement(
      MaterialPageRoute(
        builder: (ctx) => const DashboardScreen(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF1E3A8A);

    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 32.0),
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // App Brand Icon & Titles
                  Center(
                    child: Container(
                      width: 72,
                      height: 72,
                      decoration: BoxDecoration(
                        color: primaryColor,
                        borderRadius: BorderRadius.circular(16),
                        boxShadow: [
                          BoxShadow(
                            color: primaryColor.withValues(alpha: 0.25),
                            blurRadius: 16,
                            offset: const Offset(0, 6),
                          )
                        ],
                      ),
                      child: const Icon(
                        Icons.egg_alt_outlined,
                        size: 40,
                        color: Colors.white,
                      ),
                    ),
                  ),
                  const SizedBox(height: 18),
                  const Text(
                    'AERIE FARMER',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 0.5,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Offline-First Farm Production & Observation Client',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 13,
                      color: Color(0xFF64748B),
                    ),
                  ),
                  const SizedBox(height: 32),

                  // Card Container
                  Container(
                    padding: const EdgeInsets.all(24.0),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                      boxShadow: const [
                        BoxShadow(
                          color: Color(0x0A000000),
                          blurRadius: 10,
                          offset: Offset(0, 4),
                        )
                      ],
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        if (_errorMessage != null) ...[
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: const Color(0xFFFEF2F2),
                              borderRadius: BorderRadius.circular(8),
                              border: Border.all(color: const Color(0xFFFECACA)),
                            ),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Icon(Icons.error_outline, color: Color(0xFFDC2626), size: 20),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    _errorMessage!,
                                    style: const TextStyle(
                                      color: Color(0xFF991B1B),
                                      fontSize: 12,
                                      height: 1.3,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 16),
                        ],

                        // Server URL
                        TextFormField(
                          controller: _serverUrlController,
                          decoration: const InputDecoration(
                            labelText: 'AERIE Cloud Gateway URL',
                            hintText: 'http://10.0.2.2:5000/api',
                            prefixIcon: Icon(Icons.dns_outlined, size: 20),
                            border: OutlineInputBorder(),
                            contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                          ),
                          validator: (v) => v == null || v.isEmpty ? 'API URL is required' : null,
                        ),
                        const SizedBox(height: 16),

                        // Email
                        TextFormField(
                          controller: _emailController,
                          keyboardType: TextInputType.emailAddress,
                          decoration: const InputDecoration(
                            labelText: 'Farmer Account Email',
                            hintText: 'farmer@albaraka.com',
                            prefixIcon: Icon(Icons.email_outlined, size: 20),
                            border: OutlineInputBorder(),
                            contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                          ),
                          validator: (v) => v == null || !v.contains('@') ? 'Valid email required' : null,
                        ),
                        const SizedBox(height: 16),

                        // Password
                        TextFormField(
                          controller: _passwordController,
                          obscureText: true,
                          decoration: const InputDecoration(
                            labelText: 'Password',
                            prefixIcon: Icon(Icons.lock_outline, size: 20),
                            border: OutlineInputBorder(),
                            contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                          ),
                          validator: (v) => v == null || v.length < 6 ? 'Password is required' : null,
                        ),
                        const SizedBox(height: 24),

                        // Submit Button
                        ElevatedButton(
                          onPressed: _isLoading ? null : _handleLogin,
                          style: ElevatedButton.styleFrom(
                            backgroundColor: primaryColor,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                            elevation: 0,
                          ),
                          child: _isLoading
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Text(
                                  'Sign In & Synchronize',
                                  style: TextStyle(
                                    fontSize: 15,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                        ),
                        const SizedBox(height: 12),

                        // Offline Mode direct button
                        OutlinedButton.icon(
                          onPressed: _enterOfflineMode,
                          icon: const Icon(Icons.offline_bolt_outlined, size: 18),
                          label: const Text('Enter Local Field Mode (Offline)'),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: const Color(0xFF475569),
                            side: const BorderSide(color: Color(0xFFCBD5E1)),
                            padding: const EdgeInsets.symmetric(vertical: 12),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 20),

                  // Quick test prefill
                  TextButton.icon(
                    onPressed: _prefillFarmerCredentials,
                    icon: const Icon(Icons.flash_on_outlined, size: 16, color: Color(0xFF2563EB)),
                    label: const Text(
                      'Prefill Verified Farmer Credentials',
                      style: TextStyle(color: Color(0xFF2563EB), fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
