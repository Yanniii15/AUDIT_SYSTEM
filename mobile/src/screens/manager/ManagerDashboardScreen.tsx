import React, { useEffect, useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from "react-native";
import { useAuth } from "../../context/AuthContext";
import { api } from "../../services/api";
import { ManagerDashboardResponse } from "../../types";
import {
  ShieldCheck,
  TrendingUp,
  ArrowDownLeft,
  ArrowUpRight,
  Send,
  RotateCcw,
  ClipboardList,
  LogOut,
  ChevronRight,
  Sparkles,
  Coins,
} from "lucide-react-native";

export const ManagerDashboardScreen: React.FC<{ navigation: { navigate: (screen: string) => void } }> = ({ navigation }) => {
  const { user, logout } = useAuth();
  const [data, setData] = useState<ManagerDashboardResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);

  const fetchDashboard = async () => {
    try {
      const res = await api.get<ManagerDashboardResponse>("/api/manager/dashboard");
      setData(res.data);
    } catch {
      // fallback
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  };

  useEffect(() => {
    fetchDashboard();
  }, []);

  const onRefresh = () => {
    setIsRefreshing(true);
    fetchDashboard();
  };

  return (
    <View className="flex-1 bg-surface">
      {/* Top Header */}
      <View className="pt-12 pb-3 px-5 bg-surface-card border-b border-border-hairline flex-row items-center justify-between">
        <View className="space-y-0.5">
          <View className="flex-row items-center gap-1.5">
            <View className="px-2 py-0.5 rounded-full bg-primary/10 border border-primary/20">
              <Text className="text-[10px] font-bold text-primary uppercase">Executive View</Text>
            </View>
            <Text className="text-[11px] text-text-secondary font-medium">• {data?.branchName || "Main Facility"}</Text>
          </View>
          <Text className="text-xl font-extrabold text-on-surface tracking-tight">PCF Float &amp; Treasury</Text>
        </View>

        <TouchableOpacity
          onPress={logout}
          className="w-10 h-10 rounded-xl bg-surface-container-low items-center justify-center border border-border-hairline"
        >
          <LogOut size={18} color="#005f37" />
        </TouchableOpacity>
      </View>

      <ScrollView
        className="flex-1 px-4 py-4 space-y-4"
        refreshControl={<RefreshControl refreshing={isRefreshing} onRefresh={onRefresh} tintColor="#005f37" />}
      >
        {isLoading ? (
          <View className="py-12 items-center">
            <ActivityIndicator size="large" color="#005f37" />
          </View>
        ) : (
          <>
            {/* 1. PCF Float Hero Card (matching pcf_suite_executive_dashboard) */}
            <View className="bg-surface-card rounded-2xl p-5 border border-border-hairline shadow-sm space-y-4">
              <View className="flex-row items-start justify-between">
                <View>
                  <Text className="text-[10px] font-bold uppercase tracking-wider text-text-secondary">
                    Current PCF Float Position
                  </Text>
                  <Text className="text-3xl font-extrabold text-primary tracking-tight mt-1">
                    ₱{(data?.currentPcf || 0).toLocaleString("en-US", { minimumFractionDigits: 2 })}
                  </Text>
                </View>

                <View className="px-2.5 py-1 rounded-full bg-status-success-bg border border-status-success/20 flex-row items-center gap-1">
                  <View className="w-1.5 h-1.5 rounded-full bg-status-success" />
                  <Text className="text-[10px] font-bold text-status-success uppercase">
                    {data?.isSafeFloat ? "Safe Float" : "Low Float"}
                  </Text>
                </View>
              </View>

              {/* Movement Metrics In / Out */}
              <View className="grid grid-cols-2 gap-3 p-3 bg-surface-base rounded-xl border border-border-hairline flex-row">
                <View className="flex-1 space-y-0.5">
                  <View className="flex-row items-center gap-1 text-status-success">
                    <ArrowDownLeft size={14} color="#15803D" />
                    <Text className="text-[10px] font-bold uppercase text-text-secondary">Cash In Today</Text>
                  </View>
                  <Text className="text-sm font-extrabold text-on-surface">
                    ₱{(data?.cashInToday || 0).toLocaleString("en-US", { minimumFractionDigits: 2 })}
                  </Text>
                </View>

                <View className="flex-1 space-y-0.5">
                  <View className="flex-row items-center gap-1 text-status-danger">
                    <ArrowUpRight size={14} color="#DC2626" />
                    <Text className="text-[10px] font-bold uppercase text-text-secondary">Cash Out Today</Text>
                  </View>
                  <Text className="text-sm font-extrabold text-on-surface">
                    ₱{(data?.cashOutToday || 0).toLocaleString("en-US", { minimumFractionDigits: 2 })}
                  </Text>
                </View>
              </View>
            </View>

            {/* 2. Operational Action Needed Queue */}
            <View className="space-y-3 pt-1">
              <View className="flex-row items-center justify-between px-1">
                <Text className="text-xs font-bold uppercase tracking-wider text-text-secondary">
                  Operational Queue
                </Text>
                <View className="px-2 py-0.5 rounded-full bg-status-warning-bg">
                  <Text className="text-[10px] font-bold text-status-warning uppercase">
                    {(data?.pendingApprovalsCount || 0) + (data?.pendingSurrendersCount || 0)} Action Required
                  </Text>
                </View>
              </View>

              {/* Card 1: Pending Approvals */}
              <TouchableOpacity
                onPress={() => navigation.navigate("AuditApprovals")}
                className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm flex-row items-center justify-between active:bg-surface-container-low"
              >
                <View className="flex-row items-center gap-3">
                  <View className="w-11 h-11 rounded-xl bg-status-warning-bg items-center justify-center">
                    <ClipboardList size={22} color="#B7791F" />
                  </View>
                  <View>
                    <Text className="text-sm font-extrabold text-on-surface">
                      {data?.pendingApprovalsCount || 0} Pending Audit Approvals
                    </Text>
                    <Text className="text-xs text-text-secondary">Buyer receipt batches waiting for review</Text>
                  </View>
                </View>

                <ChevronRight size={18} color="#61706A" />
              </TouchableOpacity>

              {/* Card 2: Cash Surrenders */}
              <View className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm flex-row items-center justify-between">
                <View className="flex-row items-center gap-3">
                  <View className="w-11 h-11 rounded-xl bg-status-info-bg items-center justify-center">
                    <Coins size={22} color="#2563EB" />
                  </View>
                  <View>
                    <Text className="text-sm font-extrabold text-on-surface">
                      {data?.pendingSurrendersCount || 0} Cash Surrenders
                    </Text>
                    <Text className="text-xs text-text-secondary">Returned buyer floats for treasury return</Text>
                  </View>
                </View>

                <ChevronRight size={18} color="#61706A" />
              </View>
            </View>
          </>
        )}
      </ScrollView>
    </View>
  );
};
