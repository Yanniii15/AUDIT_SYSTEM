import React, { useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Image,
  Alert,
} from "react-native";
import { useAuth } from "../../context/AuthContext";
import * as ImagePicker from "expo-image-picker";
import {
  Camera,
  ImagePlus,
  Trash2,
  Calendar,
  Building2,
  CheckCircle2,
} from "lucide-react-native";

export const DailySalesUploadScreen: React.FC = () => {
  const { user } = useAuth();
  const [section, setSection] = useState<"1" | "0">("0"); // 1 = Opening, 0 = Closing
  const [photos, setPhotos] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const today = new Date().toISOString().split("T")[0];

  const handlePickImage = async () => {
    if (photos.length >= 5) {
      Alert.alert("Limit Reached", "You can upload up to 5 images per daily sales report.");
      return;
    }

    const { status } = await ImagePicker.requestCameraPermissionsAsync();
    if (status !== "granted") {
      Alert.alert("Permission Required", "Camera access is needed to capture logbook photos.");
      return;
    }

    const result = await ImagePicker.launchCameraAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      quality: 0.8,
    });

    if (!result.canceled && result.assets && result.assets.length > 0) {
      setPhotos((prev) => [...prev, result.assets[0].uri]);
    }
  };

  const handleRemovePhoto = (index: number) => {
    setPhotos((prev) => prev.filter((_, idx) => idx !== index));
  };

  const handleSubmit = async () => {
    if (photos.length === 0) {
      Alert.alert("Photo Required", "Please take at least one photo of the cashier logbook.");
      return;
    }

    setIsSubmitting(true);
    try {
      // Simulate submission & offline sync queue
      setTimeout(() => {
        setIsSubmitting(false);
        setPhotos([]);
        Alert.alert(
          "Submitted Successfully",
          "Daily sales logbook has been uploaded and queued for manager verification."
        );
      }, 1200);
    } catch {
      setIsSubmitting(false);
      Alert.alert("Error", "Could not submit report. Please try again.");
    }
  };

  return (
    <View className="flex-1 bg-surface">
      {/* Top Header */}
      <View className="pt-12 pb-3 px-5 bg-surface-card border-b border-border-hairline">
        <View className="flex-row items-center gap-1.5">
          <Building2 size={13} color="#005f37" />
          <Text className="text-[11px] font-bold text-primary uppercase">
            {user?.branchName || "Operating Branch"}
          </Text>
        </View>
        <Text className="text-xl font-extrabold text-on-surface tracking-tight">
          Upload Daily Sales Report
        </Text>
      </View>

      <ScrollView className="flex-1 px-4 py-4 space-y-4">
        {/* Section Selector */}
        <View className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm space-y-2">
          <Text className="text-[11px] font-bold uppercase tracking-wider text-text-secondary">
            Logbook Section
          </Text>
          <View className="flex-row gap-2">
            <TouchableOpacity
              onPress={() => setSection("1")}
              className={`flex-1 h-11 rounded-xl items-center justify-center border ${
                section === "1" ? "bg-primary border-primary" : "bg-surface-base border-border-hairline"
              }`}
            >
              <Text
                className={`text-xs font-bold uppercase ${
                  section === "1" ? "text-on-primary" : "text-on-surface"
                }`}
              >
                Opening Float Log
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              onPress={() => setSection("0")}
              className={`flex-1 h-11 rounded-xl items-center justify-center border ${
                section === "0" ? "bg-primary border-primary" : "bg-surface-base border-border-hairline"
              }`}
            >
              <Text
                className={`text-xs font-bold uppercase ${
                  section === "0" ? "text-on-primary" : "text-on-surface"
                }`}
              >
                Closing Sales Log
              </Text>
            </TouchableOpacity>
          </View>
        </View>

        {/* Date Info */}
        <View className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm space-y-3">
          <View className="flex-row items-center justify-between">
            <Text className="text-[11px] font-bold uppercase tracking-wider text-text-secondary">
              Business Date
            </Text>
            <View className="flex-row items-center gap-1">
              <Calendar size={13} color="#005f37" />
              <Text className="text-xs font-bold text-on-surface">{today}</Text>
            </View>
          </View>

          <View className="flex-row items-center justify-between border-t border-border-hairline pt-2">
            <Text className="text-[11px] font-bold uppercase tracking-wider text-text-secondary">
              Encoded By
            </Text>
            <Text className="text-xs font-bold text-on-surface">{user?.name}</Text>
          </View>
        </View>

        {/* Photo Intake */}
        <View className="bg-surface-card rounded-2xl p-4 border border-border-hairline shadow-sm space-y-3">
          <View className="flex-row items-center justify-between">
            <Text className="text-[11px] font-bold uppercase tracking-wider text-text-secondary">
              Logbook Photos ({photos.length}/5)
            </Text>
            <Text className="text-[10px] text-text-secondary">Attach clear reading slips</Text>
          </View>

          <TouchableOpacity
            onPress={handlePickImage}
            className="w-full h-32 rounded-xl border-2 border-dashed border-primary/40 bg-primary/5 items-center justify-center space-y-1.5"
          >
            <Camera size={32} color="#005f37" />
            <Text className="text-xs font-bold text-primary uppercase tracking-wider">
              Snap Logbook Page with Camera
            </Text>
            <Text className="text-[10px] text-text-secondary">Supports physical registers and paper sheets</Text>
          </TouchableOpacity>

          {photos.length > 0 && (
            <View className="flex-row flex-wrap gap-2 pt-1">
              {photos.map((uri, idx) => (
                <View
                  key={idx}
                  className="relative w-24 h-28 rounded-xl overflow-hidden border border-border-hairline bg-surface-base"
                >
                  <Image source={{ uri }} className="w-full h-full" resizeMode="cover" />
                  <TouchableOpacity
                    onPress={() => handleRemovePhoto(idx)}
                    className="absolute top-1 right-1 p-1 bg-black/70 rounded-full"
                  >
                    <Trash2 size={12} color="#ffffff" />
                  </TouchableOpacity>
                  <View className="absolute bottom-1 left-1 px-1.5 py-0.5 bg-black/60 rounded">
                    <Text className="text-[9px] font-bold text-white">#{idx + 1}</Text>
                  </View>
                </View>
              ))}
            </View>
          )}
        </View>

        {/* Submit Button */}
        <TouchableOpacity
          onPress={handleSubmit}
          disabled={isSubmitting}
          className="h-12 bg-primary rounded-xl items-center justify-center flex-row shadow-sm mt-2 mb-8 active:opacity-90"
        >
          {isSubmitting ? (
            <ActivityIndicator color="#ffffff" />
          ) : (
            <Text className="text-xs font-bold text-on-primary uppercase tracking-wider">
              Upload and Submit to Manager
            </Text>
          )}
        </TouchableOpacity>
      </ScrollView>
    </View>
  );
};
