import React, { useEffect, useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Image,
  Modal,
  TextInput,
  Alert,
} from "react-native";
import { useAuth } from "../../context/AuthContext";
import { api } from "../../services/api";
import { AuditsByDateResponse, AuditItemDto, AuditItemDetailDto } from "../../types";
import {
  ChevronLeft,
  ChevronRight,
  Calendar,
  Image as ImageIcon,
  CheckCircle2,
  AlertCircle,
  LogOut,
  Maximize2,
  MapPin,
  Clock,
  Pencil,
  X,
  Plus,
} from "lucide-react-native";

export const AuditorEditScreen: React.FC = () => {
  const { user, logout } = useAuth();
  const [selectedDate, setSelectedDate] = useState<string>("2026-08-27");
  const [data, setData] = useState<AuditsByDateResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [selectedPhotos, setSelectedPhotos] = useState<Record<number, string>>({});
  const [editingAudit, setEditingAudit] = useState<AuditItemDto | null>(null);
  const [editDetails, setEditDetails] = useState<AuditItemDetailDto[]>([]);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const fetchAudits = async (targetDate: string) => {
    setIsLoading(true);
    try {
      const res = await api.get<AuditsByDateResponse>(`/api/audits/by-date?date=${targetDate}`);
      setData(res.data);
      setSelectedDate(res.data.selectedDate);
    } catch {
      // fallback
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchAudits(selectedDate);
  }, []);

  const handleStepDate = (days: number) => {
    const current = new Date(selectedDate);
    current.setDate(current.getDate() + days);
    const newDateStr = current.toISOString().split("T")[0];
    setSelectedDate(newDateStr);
    fetchAudits(newDateStr);
  };

  const handleOpenEdit = (audit: AuditItemDto) => {
    setEditingAudit(audit);
    setEditDetails([...audit.details]);
  };

  const handleSaveAudit = async () => {
    if (!editingAudit) return;
    setIsSaving(true);
    try {
      await api.put(`/api/audits/${editingAudit.id}`, {
        description: editingAudit.description,
        notes: editingAudit.notes,
        details: editDetails.map((d) => ({
          id: d.id,
          itemName: d.itemName,
          quantity: d.quantity,
          price: d.price,
          expenseSourceName: d.source,
          allocationNotes: d.allocation,
        })),
      });
      setEditingAudit(null);
      fetchAudits(selectedDate);
      Alert.alert("Saved", "Receipt lines updated successfully.");
    } catch {
      Alert.alert("Error", "Could not save changes. Please try again.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <View className="flex-1 bg-surface">
      {/* Top Header */}
      <View className="pt-12 pb-3 px-5 bg-surface-card border-b border-border-hairline flex-row items-center justify-between">
        <View className="space-y-0.5">
          <View className="flex-row items-center gap-1.5">
            <View className="px-2 py-0.5 rounded-full bg-primary/10 border border-primary/20">
              <Text className="text-[10px] font-bold text-primary uppercase">Auditor Portal</Text>
            </View>
            <Text className="text-[11px] text-text-secondary font-medium">• {user?.name}</Text>
          </View>
          <Text className="text-xl font-extrabold text-on-surface tracking-tight">Audit Corrections</Text>
        </View>

        <TouchableOpacity
          onPress={logout}
          className="w-10 h-10 rounded-xl bg-surface-container-low items-center justify-center border border-border-hairline"
        >
          <LogOut size={18} color="#005f37" />
        </TouchableOpacity>
      </View>

      <ScrollView className="flex-1 px-4 py-4 space-y-4">
        {/* Date Stepper Bar */}
        <View className="bg-surface-card rounded-2xl p-4 shadow-sm border border-border-hairline space-y-3">
          <View className="flex-row items-center justify-between">
            <TouchableOpacity
              onPress={() => handleStepDate(-1)}
              className="w-10 h-10 rounded-xl bg-surface-container-low items-center justify-center border border-border-hairline"
            >
              <ChevronLeft size={20} color="#141e18" />
            </TouchableOpacity>

            <View className="flex-row items-center gap-2">
              <Calendar size={18} color="#005f37" />
              <Text className="text-sm font-bold text-on-surface">{selectedDate}</Text>
            </View>

            <TouchableOpacity
              onPress={() => handleStepDate(1)}
              className="w-10 h-10 rounded-xl bg-surface-container-low items-center justify-center border border-border-hairline"
            >
              <ChevronRight size={20} color="#141e18" />
            </TouchableOpacity>
          </View>

          {/* Quick Active Date Chips */}
          {data?.recentActiveDates && data.recentActiveDates.length > 0 && (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} className="flex-row gap-2 pt-1">
              {data.recentActiveDates.map((rd) => {
                const isCurrent = rd.date === selectedDate;
                return (
                  <TouchableOpacity
                    key={rd.date}
                    onPress={() => {
                      setSelectedDate(rd.date);
                      fetchAudits(rd.date);
                    }}
                    className={`px-3 py-1.5 rounded-xl border mr-1.5 flex-row items-center gap-1 ${
                      isCurrent
                        ? "bg-primary border-primary"
                        : "bg-surface-container-low border-border-hairline"
                    }`}
                  >
                    <Text
                      className={`text-xs font-bold uppercase ${
                        isCurrent ? "text-on-primary" : "text-on-surface"
                      }`}
                    >
                      {rd.label}
                    </Text>
                    <Text
                      className={`text-[10px] ${
                        isCurrent ? "text-on-primary-container" : "text-text-secondary"
                      }`}
                    >
                      ({rd.count})
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </ScrollView>
          )}
        </View>

        {/* Daily Stats Grid */}
        {data && (
          <View className="flex-row gap-3">
            <View className="flex-1 bg-surface-card rounded-2xl p-3.5 border border-border-hairline shadow-sm">
              <Text className="text-[10px] font-bold text-text-secondary uppercase">Total Audited</Text>
              <Text className="text-base font-extrabold text-primary mt-1">
                ₱{data.stats.totalAmount.toLocaleString("en-US", { minimumFractionDigits: 2 })}
              </Text>
              <Text className="text-[10px] text-text-secondary mt-0.5">
                {data.stats.totalReceiptPhotos} receipt photo(s)
              </Text>
            </View>

            <View className="flex-1 bg-surface-card rounded-2xl p-3.5 border border-border-hairline shadow-sm">
              <Text className="text-[10px] font-bold text-text-secondary uppercase">Submissions</Text>
              <Text className="text-base font-extrabold text-on-surface mt-1">
                {data.stats.totalCount} Audits
              </Text>
              <Text className="text-[10px] text-status-success font-semibold mt-0.5">
                {data.stats.approvedCount} approved
              </Text>
            </View>
          </View>
        )}

        {/* Audit List Content */}
        {isLoading ? (
          <View className="py-12 items-center">
            <ActivityIndicator size="large" color="#005f37" />
          </View>
        ) : !data || data.audits.length === 0 ? (
          <View className="bg-surface-card rounded-2xl p-8 border border-border-hairline items-center space-y-3">
            <View className="w-12 h-12 rounded-xl bg-surface-container-low items-center justify-center">
              <Calendar size={24} color="#61706A" />
            </View>
            <Text className="text-base font-bold text-on-surface text-center">
              No Audits on {selectedDate}
            </Text>
            <Text className="text-xs text-text-secondary text-center max-w-xs">
              Use the arrow buttons above or tap a date chip to view uploaded receipts.
            </Text>
          </View>
        ) : (
          <View className="space-y-4 pb-6">
            {data.audits.map((audit) => {
              const allImages =
                audit.images && audit.images.length > 0
                  ? audit.images
                  : audit.receiptImageUrl
                  ? [audit.receiptImageUrl]
                  : [];
              const activeImage = selectedPhotos[audit.id] || allImages[0] || null;

              return (
                <View
                  key={audit.id}
                  className="bg-surface-card rounded-2xl border border-border-hairline shadow-sm overflow-hidden space-y-3"
                >
                  {/* Card Header */}
                  <View className="bg-surface-container-low px-4 py-3 border-b border-border-hairline flex-row items-center justify-between">
                    <View className="flex-row items-center gap-2">
                      <Text className="text-xs font-bold text-on-surface">#AUD-{audit.id}</Text>
                      <View className="px-2 py-0.5 rounded-full bg-primary/10 border border-primary/20">
                        <Text className="text-[9px] font-bold text-primary uppercase">
                          {allImages.length} Receipt Photo{allImages.length === 1 ? "" : "s"}
                        </Text>
                      </View>
                    </View>

                    <Text className="text-sm font-extrabold text-primary">
                      ₱{audit.amount.toLocaleString("en-US", { minimumFractionDigits: 2 })}
                    </Text>
                  </View>

                  {/* Buyer & Branch Meta */}
                  <View className="px-4 flex-row items-center justify-between text-xs">
                    <View className="flex-row items-center gap-1">
                      <Text className="text-[11px] text-text-secondary">Buyer:</Text>
                      <Text className="text-[11px] font-bold text-on-surface">{audit.buyer.name}</Text>
                    </View>
                    <View className="flex-row items-center gap-1">
                      <MapPin size={12} color="#005f37" />
                      <Text className="text-[11px] font-bold text-on-surface">{audit.establishment.name}</Text>
                    </View>
                  </View>

                  {/* Receipt Image Gallery Preview */}
                  {allImages.length > 0 && activeImage && (
                    <View className="px-4 space-y-2">
                      <TouchableOpacity
                        onPress={() => activeImage && setPreviewImage(activeImage)}
                        className="relative w-full h-44 rounded-xl overflow-hidden bg-surface-container-low border border-border-hairline items-center justify-center"
                      >
                        <Image
                          source={{ uri: activeImage }}
                          className="w-full h-full"
                          resizeMode="cover"
                        />
                        <View className="absolute bottom-2 right-2 px-2 py-1 bg-black/70 rounded-md flex-row items-center gap-1">
                          <Maximize2 size={12} color="#ffffff" />
                          <Text className="text-[10px] font-bold text-white uppercase">Zoom</Text>
                        </View>
                      </TouchableOpacity>

                      {/* Thumbnails row when multiple photos */}
                      {allImages.length > 1 && (
                        <ScrollView horizontal showsHorizontalScrollIndicator={false} className="flex-row gap-2">
                          {allImages.map((img, idx) => (
                            <TouchableOpacity
                              key={idx}
                              onPress={() => setSelectedPhotos({ ...selectedPhotos, [audit.id]: img })}
                              className={`w-12 h-12 rounded-lg overflow-hidden border mr-2 ${
                                activeImage === img ? "border-primary border-2" : "border-border-hairline"
                              }`}
                            >
                              <Image source={{ uri: img }} className="w-full h-full" resizeMode="cover" />
                            </TouchableOpacity>
                          ))}
                        </ScrollView>
                      )}
                    </View>
                  )}

                  {/* Line Items List */}
                  <View className="px-4 space-y-2">
                    <Text className="text-[10px] font-bold uppercase tracking-wider text-text-secondary">
                      Itemized Lines ({audit.details.length})
                    </Text>
                    <View className="bg-surface-base rounded-xl border border-border-hairline overflow-hidden divide-y divide-border-hairline">
                      {audit.details.map((d, dIdx) => (
                        <View key={dIdx} className="p-3 space-y-1">
                          <View className="flex-row justify-between items-start">
                            <Text className="text-xs font-bold text-on-surface flex-1 mr-2">{d.itemName}</Text>
                            <Text className="text-xs font-extrabold text-primary">
                              ₱{d.total.toLocaleString("en-US", { minimumFractionDigits: 2 })}
                            </Text>
                          </View>
                          <View className="flex-row justify-between items-center text-[10px]">
                            <Text className="text-text-secondary">
                              Source: <Text className="font-bold text-on-surface">{d.source || "—"}</Text>
                            </Text>
                            <Text className="text-text-secondary">
                              Alloc: <Text className="font-bold text-on-surface">{d.allocation || "—"}</Text>
                            </Text>
                          </View>
                        </View>
                      ))}
                    </View>
                  </View>

                  {/* Action Bar */}
                  <View className="px-4 pb-4 pt-1 flex-row gap-2">
                    <TouchableOpacity
                      onPress={() => handleOpenEdit(audit)}
                      className="flex-1 h-11 bg-primary rounded-xl items-center justify-center flex-row gap-2 shadow-sm"
                    >
                      <Pencil size={16} color="#ffffff" />
                      <Text className="text-xs font-bold text-on-primary uppercase tracking-wider">
                        Edit Receipt &amp; Allocations
                      </Text>
                    </TouchableOpacity>
                  </View>
                </View>
              );
            })}
          </View>
        )}
      </ScrollView>

      {/* Edit Modal */}
      <Modal visible={!!editingAudit} animationType="slide" transparent>
        <View className="flex-1 justify-end bg-black/60">
          <View className="bg-surface-card rounded-t-3xl p-6 max-h-[85%] space-y-4">
            <View className="flex-row items-center justify-between border-b border-border-hairline pb-3">
              <Text className="text-base font-extrabold text-on-surface">
                Edit Audit #{editingAudit?.id}
              </Text>
              <TouchableOpacity onPress={() => setEditingAudit(null)}>
                <X size={20} color="#141e18" />
              </TouchableOpacity>
            </View>

            <ScrollView className="space-y-4">
              {editDetails.map((detail, idx) => (
                <View key={idx} className="bg-surface-base p-4 rounded-xl border border-border-hairline space-y-3">
                  <Text className="text-[10px] font-bold uppercase text-primary">Line #{idx + 1}</Text>
                  <View className="space-y-1">
                    <Text className="text-[10px] font-bold text-text-secondary uppercase">Item Name</Text>
                    <TextInput
                      value={detail.itemName}
                      onChangeText={(val) => {
                        const copy = [...editDetails];
                        copy[idx].itemName = val;
                        setEditDetails(copy);
                      }}
                      className="h-10 bg-surface-card border border-border-hairline rounded-lg px-3 text-xs text-on-surface font-semibold"
                    />
                  </View>

                  <View className="flex-row gap-2">
                    <View className="flex-1 space-y-1">
                      <Text className="text-[10px] font-bold text-text-secondary uppercase">Qty</Text>
                      <TextInput
                        value={String(detail.quantity)}
                        keyboardType="numeric"
                        onChangeText={(val) => {
                          const copy = [...editDetails];
                          copy[idx].quantity = parseInt(val) || 1;
                          copy[idx].total = copy[idx].quantity * copy[idx].price;
                          setEditDetails(copy);
                        }}
                        className="h-10 bg-surface-card border border-border-hairline rounded-lg px-3 text-xs text-on-surface font-semibold"
                      />
                    </View>

                    <View className="flex-1 space-y-1">
                      <Text className="text-[10px] font-bold text-text-secondary uppercase">Unit Price</Text>
                      <TextInput
                        value={String(detail.price)}
                        keyboardType="numeric"
                        onChangeText={(val) => {
                          const copy = [...editDetails];
                          copy[idx].price = parseFloat(val) || 0;
                          copy[idx].total = copy[idx].quantity * copy[idx].price;
                          setEditDetails(copy);
                        }}
                        className="h-10 bg-surface-card border border-border-hairline rounded-lg px-3 text-xs text-on-surface font-semibold"
                      />
                    </View>
                  </View>

                  <View className="space-y-1">
                    <Text className="text-[10px] font-bold text-text-secondary uppercase">Vendor / Source</Text>
                    <TextInput
                      value={detail.source}
                      onChangeText={(val) => {
                        const copy = [...editDetails];
                        copy[idx].source = val;
                        setEditDetails(copy);
                      }}
                      className="h-10 bg-surface-card border border-border-hairline rounded-lg px-3 text-xs text-on-surface font-semibold"
                    />
                  </View>

                  <View className="space-y-1">
                    <Text className="text-[10px] font-bold text-text-secondary uppercase">Allocation Notes</Text>
                    <TextInput
                      value={detail.allocation}
                      onChangeText={(val) => {
                        const copy = [...editDetails];
                        copy[idx].allocation = val;
                        setEditDetails(copy);
                      }}
                      className="h-10 bg-surface-card border border-border-hairline rounded-lg px-3 text-xs text-on-surface font-semibold"
                    />
                  </View>
                </View>
              ))}
            </ScrollView>

            <TouchableOpacity
              onPress={handleSaveAudit}
              disabled={isSaving}
              className="h-12 bg-primary rounded-xl items-center justify-center flex-row shadow-sm"
            >
              {isSaving ? (
                <ActivityIndicator color="#ffffff" />
              ) : (
                <Text className="text-xs font-bold text-on-primary uppercase tracking-wider">Save All Line Items</Text>
              )}
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
