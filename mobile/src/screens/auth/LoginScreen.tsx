import React, { useState } from "react";
import axios from "axios";
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  ScrollView,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { useAuth } from "../../context/AuthContext";
import { ShieldCheck, Mail, Lock, Building2 } from "lucide-react-native";

export const LoginScreen: React.FC = () => {
  const { login, isLoading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleLogin = async (loginEmail?: string, loginPassword?: string) => {
    const targetEmail = loginEmail || email;
    const targetPass = loginPassword || password;

    if (!targetEmail || !targetPass) {
      setErrorMsg("Please enter both email and password.");
      return;
    }

    setErrorMsg(null);
    try {
      await login(targetEmail, targetPass);
    } catch (err: unknown) {
      let msg = "Invalid credentials. Please check and try again.";
      if (axios.isAxiosError(err) && err.response?.data?.message) {
        msg = String(err.response.data.message);
      }
      setErrorMsg(msg);
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      className="flex-1 bg-surface"
    >
      <ScrollView
        contentContainerStyle={{ flexGrow: 1, justifyContent: "center" }}
        className="px-6 py-12"
      >
        <View className="w-full max-w-md mx-auto space-y-6">
          {/* Header Brand */}
          <View className="items-center space-y-2 mb-2">
            <View className="w-16 h-16 rounded-2xl bg-primary/10 items-center justify-center mb-1">
              <ShieldCheck size={36} color="#005f37" />
            </View>
            <View className="flex-row items-center gap-1.5 px-3 py-1 rounded-full bg-surface-container border border-border-hairline">
              <Building2 size={13} color="#005f37" />
              <Text className="text-[11px] font-bold tracking-wider text-primary uppercase">
                AuditCkDayo Mobile
              </Text>
            </View>
            <Text className="text-2xl font-extrabold text-on-surface text-center tracking-tight">
              Sign In to Your Portal
            </Text>
            <Text className="text-xs text-text-secondary text-center max-w-xs">
              Petty Cash Fund auditing, receipt inspections, and branch daily sales management.
            </Text>
          </View>

          {/* Form Card */}
          <View className="bg-surface-card rounded-2xl p-6 shadow-sm border border-border-hairline space-y-4">
            {errorMsg && (
              <View className="p-3 bg-status-danger-bg border border-status-danger/20 rounded-xl">
                <Text className="text-xs text-status-danger font-semibold text-center">
                  {errorMsg}
                </Text>
              </View>
            )}

            {/* Email Field */}
            <View className="space-y-1.5">
              <Text className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                Email Address
              </Text>
              <View className="flex-row items-center h-12 bg-surface-base border border-border-hairline rounded-xl px-3.5 focus:border-primary">
                <Mail size={18} color="#61706A" />
                <TextInput
                  value={email}
                  onChangeText={setEmail}
                  placeholder="Enter email..."
                  placeholderTextColor="#9ca3af"
                  autoCapitalize="none"
                  keyboardType="email-address"
                  className="flex-1 ml-2.5 text-sm text-on-surface font-medium"
                />
              </View>
            </View>

            {/* Password Field */}
            <View className="space-y-1.5">
              <Text className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                Password
              </Text>
              <View className="flex-row items-center h-12 bg-surface-base border border-border-hairline rounded-xl px-3.5 focus:border-primary">
                <Lock size={18} color="#61706A" />
                <TextInput
                  value={password}
                  onChangeText={setPassword}
                  placeholder="••••••••"
                  placeholderTextColor="#9ca3af"
                  secureTextEntry
                  className="flex-1 ml-2.5 text-sm text-on-surface font-medium"
                />
              </View>
            </View>

            {/* Submit Button */}
            <TouchableOpacity
              onPress={() => handleLogin()}
              disabled={isLoading}
              className="h-12 bg-primary rounded-xl items-center justify-center flex-row shadow-sm active:opacity-90 mt-2"
            >
              {isLoading ? (
                <ActivityIndicator color="#ffffff" />
              ) : (
                <Text className="text-sm font-bold text-on-primary uppercase tracking-wider">
                  Authenticate &amp; Enter
                </Text>
              )}
            </TouchableOpacity>
          </View>

          {/* Quick Demo Logins */}
          <View className="space-y-2 pt-2">
            <Text className="text-[10px] font-bold uppercase tracking-wider text-text-secondary text-center">
              Quick Role Presets (Testing)
            </Text>
            <View className="flex-row gap-2 justify-center">
              <TouchableOpacity
                onPress={() => {
                  setEmail("auditor1@test.com");
                  setPassword("Password123!");
                  handleLogin("auditor1@test.com", "Password123!");
                }}
                className="px-3 py-2 rounded-xl bg-surface-card border border-border-hairline items-center"
              >
                <Text className="text-[10px] font-bold text-primary uppercase">Auditor</Text>
              </TouchableOpacity>

              <TouchableOpacity
                onPress={() => {
                  setEmail("manager1@test.com");
                  setPassword("Password123!");
                  handleLogin("manager1@test.com", "Password123!");
                }}
                className="px-3 py-2 rounded-xl bg-surface-card border border-border-hairline items-center"
              >
                <Text className="text-[10px] font-bold text-primary uppercase">Manager</Text>
              </TouchableOpacity>

              <TouchableOpacity
                onPress={() => {
                  setEmail("branchstaff1@test.com");
                  setPassword("Password123!");
                  handleLogin("branchstaff1@test.com", "Password123!");
                }}
                className="px-3 py-2 rounded-xl bg-surface-card border border-border-hairline items-center"
              >
                <Text className="text-[10px] font-bold text-primary uppercase">Branch Staff</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
};
