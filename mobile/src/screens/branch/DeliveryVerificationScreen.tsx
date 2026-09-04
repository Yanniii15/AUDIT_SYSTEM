import React, { useEffect, useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from "react-native";
import { useAuth } from "../../context/AuthContext";
import { api } from "../../services/api";
import { PendingDeliveryDto } from "../../types";
import {
  Truck,
  CheckCircle2,
  Calendar,
  Package,
  Building2,
  LogOut,
} from "lucide-react-native";

export const DeliveryVerificationScreen: React.FC = () => {
  const { user, logout } = useAuth();
  const [deliveries, setDeliveries] = useState<PendingDeliveryDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [verifyingId, setVerifyingId] = useState<number | null>(null);

  const fetchDeliveries = async () => {
    setIsLoading(true);
    try {
      const res = await api.get<PendingDeliveryDto[]>("/api/branch/deliveries");
      setDeliveries(res.data);
    } catch {
      // fallback
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDeliveries();
  }, []);

  const handleVerify = async (detailId: number, itemName: string) => {
    Alert.alert("Confirm Delivery", `Verify delivery of ${itemName}?`, [
      { text: "Cancel", style: "cancel" },
      {
        text: "Confirm Verified",
        onPress: async () => {
          setVerifyingId(detailId);
          try {
            await api.post(`/api/branch/deliveries/${detailId}/verify`);
            setDeliveries((prev) => prev.filter((d) => d.id !== detailId));
            Alert.alert("Verified", `${itemName} has been marked as delivered & verified.`);
          } catch {
            Alert.alert("Error", "Could not verify delivery. Please try again.");
          } finally {
            setVerifyingId(null);
          }
        },
      },
    ]);
  };

  return (
    <View className="flex-1 bg-surface">
      {/* Top Header */}
      <View className="pt-12 pb-3 px-5 bg-surface-card border-b border-border-hairline flex-row items-center justify-between">
        <View>
          <View className="flex-row items-center gap-1.5">
            <Building2 size={13} color="#005f37" />
            <Text className="text-[11px] font-bold text-primary uppercase">
              {user?.branchName || "Assigned Branch"}
            </Text>
          </View>
          <Text className="text-xl font-extrabold text-on-surface tracking-tight">Delivery Verification</Text>
        </View>

        <TouchableOpacity
          onPress={logout}
          className="w-10 h-10 rounded-xl bg-surface-container-low items-center justify-center border border-border-hairline"
        >
          <LogOut size={18} color="#005f37" />
        </TouchableOpacity>
      </View>

      <ScrollView className="flex-1 px-4 py-4 space-y-4">
        <View className="flex-row items-center justify-between px-1">
          <Text className="text-xs font-bold uppercase tracking-wider text-text-secondary">
            Deliveries Awaiting Receiving Check
          </Text>
          <View className="px-2.5 py-0.5 rounded-full bg-status-warning-bg border border-status-warning/20">
            <Text className="text-[10px] font-bold text-status-warning uppercase">
              {deliveries.length} Items
            </Text>
          </View>
        </View>

        {isLoading ? (
          <View className="py-12 items-center">
            <ActivityIndicator size="large" color="#005f37" />
          </View>
        ) : deliveries.length === 0 ? (
          <View className="bg-surface-card rounded-2xl p-8 border border-border-hairline items-center space-y-3">
            <CheckCircle2 size={36} color="#15803D" />
            <Text className="text-base font-bold text-on-surface">All Deliveries Verified</Text>
            <Text className="text-xs text-text-secondary text-center">
              There are no pending items waiting at the dock counter right now.
            </Text>
          </View>
        ) : (
          <View className="space-y-3 pb-8">
            {deliveries.map((delivery) => (
              <View
                key={delivery.id}
                className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm space-y-3"
              >
                <View className="flex-row justify-between items-start">
                  <View className="flex-1 mr-2">
                    <Text className="text-sm font-extrabold text-on-surface">{delivery.itemName}</Text>
                    <Text className="text-xs text-text-secondary mt-0.5">
                      Vendor: <Text className="font-bold text-on-surface">{delivery.source}</Text>
                    </Text>
                  </View>

                  <Text className="text-sm font-extrabold text-primary">
                    ₱{delivery.total.toLocaleString("en-US", { minimumFractionDigits: 2 })}
                  </Text>
                </View>

                <View className="flex-row items-center justify-between bg-surface-base p-2.5 rounded-xl border border-border-hairline text-xs">
                  <View className="flex-row items-center gap-1">
                    <Package size={14} color="#61706A" />
                    <Text className="text-[11px] font-semibold text-on-surface">
                      Qty: {delivery.quantity} &times; ₱{delivery.price.toFixed(2)}
                    </Text>
                  </View>

                  <View className="flex-row items-center gap-1">
                    <Calendar size={13} color="#61706A" />
                    <Text className="text-[11px] text-text-secondary">{delivery.date}</Text>
                  </View>
                </View>

                <TouchableOpacity
                  onPress={() => handleVerify(delivery.id, delivery.itemName)}
                  disabled={verifyingId === delivery.id}
                  className="h-11 bg-primary rounded-xl items-center justify-center flex-row gap-1.5 shadow-sm"
                >
                  {verifyingId === delivery.id ? (
                    <ActivityIndicator color="#ffffff" />
                  ) : (
                    <>
                      <CheckCircle2 size={16} color="#ffffff" />
                      <Text className="text-xs font-bold text-on-primary uppercase tracking-wider">
                        Confirm Delivery Received
                      </Text>
                    </>
                  )}
                </TouchableOpacity>
              </View>
            ))}
          </View>
        )}
      </ScrollView>
    </View>
  );
};
