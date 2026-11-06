import 'package:flutter/material.dart';
import 'database/database_helper.dart';
import 'screens/dashboard_screen.dart';
import 'screens/login_screen.dart';
import 'screens/sync_history_screen.dart';
import 'services/sync_service.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Initialize SQLite database
  await DatabaseHelper.instance.database;

  // Initialize offline sync service with default gateway
  SyncService.instance.initialize(
    apiBaseUrl: 'http://10.0.2.2:5000/api',
  );

  runApp(const AerieFarmerApp());
}

class AerieFarmerApp extends StatelessWidget {
  const AerieFarmerApp({super.key});

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF1E3A8A);

    return MaterialApp(
      title: 'AERIE Farmer Field Client',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: primaryColor,
          primary: primaryColor,
          secondary: const Color(0xFF059669),
          surface: Colors.white,
        ),
        scaffoldBackgroundColor: const Color(0xFFF8FAFC),
        appBarTheme: const AppBarTheme(
          backgroundColor: primaryColor,
          foregroundColor: Colors.white,
          elevation: 0,
          centerTitle: false,
        ),
        cardTheme: CardThemeData(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: const BorderSide(color: Color(0xFFE2E8F0)),
          ),
          color: Colors.white,
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(8),
            borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(8),
            borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(8),
            borderSide: const BorderSide(color: primaryColor, width: 1.5),
          ),
          labelStyle: const TextStyle(color: Color(0xFF475569), fontSize: 13),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            elevation: 0,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
          ),
        ),
      ),
      initialRoute: '/login',
      routes: {
        '/login': (context) => const LoginScreen(),
        '/dashboard': (context) => const DashboardScreen(),
        '/history': (context) => const SyncHistoryScreen(),
      },
    );
  }
}
