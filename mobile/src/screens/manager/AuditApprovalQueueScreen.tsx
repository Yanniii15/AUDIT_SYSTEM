import React, { useEffect, useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Image,
  Alert,
  Modal,
  TextInput,
} from "react-native";
import { api } from "../../services/api";
import {
  CheckCircle2,
  XCircle,
  Clock,
  MapPin,
  Maximize2,
  Filter,
  X,
  ShieldAlert,
} from "lucide-react-native";

interface AuditQueueItem {
  id: number;
  amount: number;
  description: string;
  entryDate: string;
  submittedAt?: string;
  buyer: { id: number; name: string; email: string };
  establishment: { id: number; name: string };
  imageUrls: string[];
  receiptImageUrl?: string;
  itemCount: number;
  details: Array<{
    id: number;
    itemName: string;
    quantity: number;
    price: number;
    total: number;
    source: string;
    allocation: string;
    hasReceipt: boolean;
  }>;
}

export const AuditApprovalQueueScreen: React.FC = () => {
  const [queue, setQueue] = useState<AuditQueueItem[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [activeFilter, setActiveFilter] = useState<"All" | "HighValue" | "Flagged">("All");
  const [rejectingItem, setRejectingItem] = useState<AuditQueueItem | null>(null);
  const [rejectReason, setRejectReason] = useState<string>("");
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<number | null>(null);

  const fetchQueue = async () => {
    setIsLoading(true);
    try {
      const res = await api.get<AuditQueueItem[]>("/api/manager/audit-queue");
      setQueue(res.data);
    } catch {
      // fallback
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchQueue();
  }, []);

  const handleApprove = async (id: number) => {
    Alert.alert("Confirm Approval", `Approve Audit #${id}?`, [
      { text: "Cancel", style: "cancel" },
      {
        text: "Approve",
        onPress: async () => {
          setActionInProgress(id);
          try {
            await api.post(`/api/manager/audits/${id}/approve`);
            setQueue((prev) => prev.filter((item) => item.id !== id));
            Alert.alert("Approved", `Audit #${id} has been marked as approved.`);
          } catch {
            Alert.alert("Error", "Could not approve audit. Please try again.");
          } finally {
            setActionInProgress(null);
          }
        },
      },
    ]);
  };

  const handleConfirmReject = async () => {
    if (!rejectingItem) return;
    const id = rejectingItem.id;
    setActionInProgress(id);
    try {
      await api.post(`/api/manager/audits/${id}/reject`, { reason: rejectReason });
      setQueue((prev) => prev.filter((item) => item.id !== id));
      setRejectingItem(null);
      setRejectReason("");
      Alert.alert("Rejected", `Audit #${id} has been rejected.`);
    } catch {
      Alert.alert("Error", "Could not reject audit. Please try again.");
    } finally {
      setActionInProgress(null);
    }
  };

  const filteredQueue = queue.filter((item) => {
    if (activeFilter === "HighValue") return item.amount >= 5000;
    if (activeFilter === "Flagged") return item.details.some((d) => !d.hasReceipt);
    return true;
  });

  return (
    <View className="flex-1 bg-surface">
      {/* Top Header */}
      <View className="pt-12 pb-3 px-5 bg-surface-card border-b border-border-hairline flex-row items-center justify-between">
        <View>
          <Text className="text-xl font-extrabold text-on-surface tracking-tight">Audit Approvals</Text>
          <Text className="text-xs text-text-secondary">Verify and approve buyer submissions</Text>
        </View>
        <View className="px-3 py-1 rounded-full bg-status-warning-bg border border-status-warning/20">
          <Text className="text-xs font-bold text-status-warning">{queue.length} Pending</Text>
        </View>
      </View>

      {/* Filter Chips Bar */}
      <View className="px-4 py-3 bg-surface border-b border-border-hairline flex-row gap-2">
        <TouchableOpacity
          onPress={() => setActiveFilter("All")}
          className={`px-3 py-1.5 rounded-full border ${
            activeFilter === "All" ? "bg-primary border-primary" : "bg-surface-card border-border-hairline"
          }`}
        >
          <Text className={`text-xs font-bold ${activeFilter === "All" ? "text-on-primary" : "text-on-surface"}`}>
            All ({queue.length})
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          onPress={() => setActiveFilter("HighValue")}
          className={`px-3 py-1.5 rounded-full border ${
            activeFilter === "HighValue" ? "bg-primary border-primary" : "bg-surface-card border-border-hairline"
          }`}
        >
          <Text
            className={`text-xs font-bold ${
              activeFilter === "HighValue" ? "text-on-primary" : "text-on-surface"
            }`}
          >
            High Value (&gt;₱5k)
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          onPress={() => setActiveFilter("Flagged")}
          className={`px-3 py-1.5 rounded-full border ${
            activeFilter === "Flagged" ? "bg-primary border-primary" : "bg-surface-card border-border-hairline"
          }`}
        >
          <Text
            className={`text-xs font-bold ${
              activeFilter === "Flagged" ? "text-on-primary" : "text-on-surface"
            }`}
          >
            Missing Receipts
          </Text>
        </TouchableOpacity>
      </View>

      <ScrollView className="flex-1 px-4 py-4 space-y-4">
        {isLoading ? (
          <View className="py-12 items-center">
            <ActivityIndicator size="large" color="#005f37" />
          </View>
        ) : filteredQueue.length === 0 ? (
          <View className="bg-surface-card rounded-2xl p-8 border border-border-hairline items-center space-y-3">
            <CheckCircle2 size={36} color="#15803D" />
            <Text className="text-base font-bold text-on-surface">Queue is Clear</Text>
            <Text className="text-xs text-text-secondary text-center">
              All audits have been verified or no records match this filter.
            </Text>
          </View>
        ) : (
          <View className="space-y-4 pb-8">
            {filteredQueue.map((audit) => {
              const photo = audit.imageUrls[0] || audit.receiptImageUrl || null;
              const hasNoReceiptLine = audit.details.some((d) => !d.hasReceipt);

              return (
                <View
                  key={audit.id}
                  className="bg-surface-card rounded-2xl border border-border-hairline shadow-sm overflow-hidden space-y-3 p-4"
                >
                  {/* Card Header */}
                  <View className="flex-row items-center justify-between">
                    <View className="flex-row items-center gap-2">
                      <Text className="text-xs font-extrabold text-on-surface">#AUD-{audit.id}</Text>
                      {hasNoReceiptLine && (
                        <View className="px-2 py-0.5 rounded-md bg-status-danger-bg flex-row items-center gap-1">
                          <ShieldAlert size={10} color="#DC2626" />
                          <Text className="text-[9px] font-bold text-status-danger uppercase">No Receipt</Text>
                        </View>
                      )}
                    </View>

                    <Text className="text-base font-extrabold text-primary">
                      ₱{audit.amount.toLocaleString("en-US", { minimumFractionDigits: 2 })}
                    </Text>
                  </View>

                  {/* Buyer & Date Details */}
                  <View className="flex-row justify-between text-xs text-text-secondary">
                    <Text className="text-[11px] font-bold text-on-surface">{audit.buyer.name}</Text>
                    <View className="flex-row items-center gap-1">
                      <MapPin size={12} color="#005f37" />
                      <Text className="text-[11px] font-medium text-on-surface">{audit.establishment.name}</Text>
                    </View>
                  </View>

                  {/* Photo & Items Row */}
                  <View className="flex-row gap-3 pt-1">
                    {photo && (
                      <TouchableOpacity
                        onPress={() => setPreviewImage(photo)}
                        className="w-20 h-24 rounded-xl overflow-hidden bg-surface-container-low border border-border-hairline shrink-0 relative"
                      >
                        <Image source={{ uri: photo }} className="w-full h-full" resizeMode="cover" />
                        <View className="absolute bottom-1 right-1 p-1 bg-black/70 rounded">
                          <Maximize2 size={10} color="#ffffff" />
                        </View>
                      </TouchableOpacity>
                    )}

                    <View className="flex-1 space-y-1.5">
                      <Text className="text-[10px] font-bold uppercase text-text-secondary tracking-wider">
                        Extracted Lines ({audit.itemCount})
                      </Text>
                      <View className="bg-surface-base p-2.5 rounded-xl space-y-1 border border-border-hairline">
                        {audit.details.slice(0, 3).map((d, idx) => (
                          <View key={idx} className="flex-row justify-between text-[11px]">
                            <Text className="text-on-surface font-semibold flex-1 mr-1 truncate">{d.itemName}</Text>
                            <Text className="text-primary font-bold">
                              ₱{d.total.toLocaleString("en-US", { minimumFractionDigits: 2 })}
                            </Text>
                          </View>
                        ))}
                        {audit.details.length > 3 && (
                          <Text className="text-[10px] text-text-secondary pt-0.5">
                            +{audit.details.length - 3} more items...
                          </Text>
                        )}
                      </View>
                    </View>
                  </View>

                  {/* Approve / Reject Actions */}
                  <View className="flex-row gap-2.5 pt-2 border-t border-border-hairline">
                    <TouchableOpacity
                      onPress={() => setRejectingItem(audit)}
                      disabled={actionInProgress === audit.id}
                      className="flex-1 h-11 bg-status-danger-bg border border-status-danger/30 rounded-xl items-center justify-center flex-row gap-1.5"
                    >
                      <XCircle size={16} color="#DC2626" />
                      <Text className="text-xs font-bold text-status-danger uppercase tracking-wider">Reject</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      onPress={() => handleApprove(audit.id)}
                      disabled={actionInProgress === audit.id}
                      className="flex-1 h-11 bg-primary rounded-xl items-center justify-center flex-row gap-1.5 shadow-sm"
                    >
                      {actionInProgress === audit.id ? (
                        <ActivityIndicator color="#ffffff" />
                      ) : (
                        <>
                          <CheckCircle2 size={16} color="#ffffff" />
                          <Text className="text-xs font-bold text-on-primary uppercase tracking-wider">Approve</Text>
                        </>
                      )}
                    </TouchableOpacity>
                  </View>
                </View>
              );
            })}
          </View>
        )}
      </ScrollView>

      {/* Reject Modal */}
      <Modal visible={!!rejectingItem} transparent animationType="slide">
        <View className="flex-1 justify-end bg-black/60">
          <View className="bg-surface-card rounded-t-3xl p-6 space-y-4">
            <View className="flex-row justify-between items-center border-b border-border-hairline pb-3">
              <Text className="text-base font-extrabold text-on-surface">
                Reject Audit #{rejectingItem?.id}
              </Text>
              <TouchableOpacity onPress={() => setRejectingItem(null)}>
                <X size={20} color="#141e18" />
              </TouchableOpacity>
            </View>

            <View className="space-y-1.5">
              <Text className="text-xs font-bold text-text-secondary uppercase">Reason for Rejection</Text>
              <TextInput
                value={rejectReason}
                onChangeText={setRejectReason}
                placeholder="e.g. Receipt price mismatch with invoice..."
                placeholderTextColor="#9ca3af"
                multiline
                numberOfLines={3}
                className="bg-surface-base border border-border-hairline rounded-xl p-3 text-sm text-on-surface"
              />
            </View>

            <TouchableOpacity
              onPress={handleConfirmReject}
              className="h-12 bg-status-danger rounded-xl items-center justify-center"
            >
              <Text className="text-xs font-bold text-white uppercase tracking-wider">
                Confirm Rejection
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      {/* Fullscreen Photo Modal */}
      <Modal visible={!!previewImage} transparent animationType="fade">
        <View className="flex-1 bg-black/90 items-center justify-center p-4">
          <TouchableOpacity
            onPress={() => setPreviewImage(null)}
            className="absolute top-12 right-6 z-10 w-10 h-10 rounded-full bg-white/20 items-center justify-center"
          >
            <X size={24} color="#ffffff" />
          </TouchableOpacity>
          {previewImage && (
            <Image source={{ uri: previewImage }} className="w-full h-[80%]" resizeMode="contain" />
          )}
        </View>
      </Modal>
    </View>
  );
};
