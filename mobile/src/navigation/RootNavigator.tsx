import React from "react";
import { View, Text, ActivityIndicator } from "react-native";
import { NavigationContainer } from "@react-navigation/native";
import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import { useAuth } from "../context/AuthContext";
import { LoginScreen } from "../screens/auth/LoginScreen";
import { AuditorEditScreen } from "../screens/auditor/AuditorEditScreen";
import { ManagerDashboardScreen } from "../screens/manager/ManagerDashboardScreen";
import { AuditApprovalQueueScreen } from "../screens/manager/AuditApprovalQueueScreen";
import { DeliveryVerificationScreen } from "../screens/branch/DeliveryVerificationScreen";
import { DailySalesUploadScreen } from "../screens/branch/DailySalesUploadScreen";
import {
  FileEdit,
  LayoutDashboard,
  ClipboardCheck,
  Truck,
  Camera,
  LogOut,
} from "lucide-react-native";

const Tab = createBottomTabNavigator();

const commonTabScreenOptions = {
  headerShown: false,
  tabBarActiveTintColor: "#005f37",
  tabBarInactiveTintColor: "#61706A",
  tabBarStyle: {
    backgroundColor: "#ffffff",
    borderTopColor: "#DCE5DF",
    height: 64,
    paddingBottom: 10,
    paddingTop: 8,
  },
  tabBarLabelStyle: {
    fontSize: 11,
    fontWeight: "700" as const,
  },
};

export const RootNavigator: React.FC = () => {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center bg-surface">
        <ActivityIndicator size="large" color="#005f37" />
        <Text className="text-xs font-bold text-text-secondary mt-3 uppercase tracking-wider">
          Loading AuditCkDayo...
        </Text>
      </View>
    );
  }

  if (!user) {
    return <LoginScreen />;
  }

  return (
    <NavigationContainer>
      {user.role === "Auditor" && (
        <Tab.Navigator screenOptions={commonTabScreenOptions}>
          <Tab.Screen
            name="AuditorEdit"
            component={AuditorEditScreen}
            options={{
              tabBarLabel: "Edit Audits",
              tabBarIcon: ({ color, size }) => <FileEdit size={size} color={color} />,
            }}
          />
        </Tab.Navigator>
      )}

      {(user.role === "Manager" || user.role === "Owner" || user.role === "Admin") && (
        <Tab.Navigator screenOptions={commonTabScreenOptions}>
          <Tab.Screen
            name="ManagerDashboard"
            component={ManagerDashboardScreen}
            options={{
              tabBarLabel: "PCF Position",
              tabBarIcon: ({ color, size }) => <LayoutDashboard size={size} color={color} />,
            }}
          />
          <Tab.Screen
            name="AuditApprovals"
            component={AuditApprovalQueueScreen}
            options={{
              tabBarLabel: "Approvals",
              tabBarIcon: ({ color, size }) => <ClipboardCheck size={size} color={color} />,
            }}
          />
        </Tab.Navigator>
      )}

      {user.role === "BranchStaff" && (
        <Tab.Navigator screenOptions={commonTabScreenOptions}>
          <Tab.Screen
            name="DeliveryVerification"
            component={DeliveryVerificationScreen}
            options={{
              tabBarLabel: "Deliveries",
              tabBarIcon: ({ color, size }) => <Truck size={size} color={color} />,
            }}
          />
          <Tab.Screen
            name="DailySalesUpload"
            component={DailySalesUploadScreen}
            options={{
              tabBarLabel: "Daily Sales",
              tabBarIcon: ({ color, size }) => <Camera size={size} color={color} />,
            }}
          />
        </Tab.Navigator>
      )}

      {user.role === "Buyer" && (
        <Tab.Navigator screenOptions={commonTabScreenOptions}>
          <Tab.Screen
            name="BuyerDelivery"
            component={DeliveryVerificationScreen}
            options={{
              tabBarLabel: "My Audits",
              tabBarIcon: ({ color, size }) => <FileEdit size={size} color={color} />,
            }}
          />
        </Tab.Navigator>
      )}
    </NavigationContainer>
  );
};
