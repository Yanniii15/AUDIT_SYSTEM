import React, { useState, useRef, useEffect } from "react";
import {
  View,
  Text,
  ActivityIndicator,
  SafeAreaView,
  StyleSheet,
  StatusBar,
  TouchableOpacity,
  BackHandler,
  Platform,
  Image,
} from "react-native";
import * as FileSystem from "expo-file-system/legacy";
import * as Sharing from "expo-sharing";
import { WebView } from "react-native-webview";

const APP_URL = "https://makbiecompanies.dev";

export default function App() {
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);
  const [canGoBack, setCanGoBack] = useState(false);
  const webViewRef = useRef<WebView>(null);

  // Handle Android hardware back button
  useEffect(() => {
    if (Platform.OS === "android") {
      const onBackPress = () => {
        if (canGoBack && webViewRef.current) {
          webViewRef.current.goBack();
          return true;
        }
        return false;
      };
      const sub = BackHandler.addEventListener("hardwareBackPress", onBackPress);
      return () => sub.remove();
    }
  }, [canGoBack]);

  const handleRetry = () => {
    setHasError(false);
    setIsLoading(true);
    webViewRef.current?.reload();
  };
  const handleMessage = async (event: any) => {
    try {
      const data = JSON.parse(event.nativeEvent.data);
      if (data && data.type === "EXPORT_IMAGE" && data.dataUrl) {
        const rawBase64 = data.dataUrl.replace(/^data:image\/\w+;base64,/, "");
        if (!rawBase64) return;
        const filename = data.filename || `Export_${Date.now()}.png`;
        const fileUri = `${FileSystem.cacheDirectory}${filename}`;
        await FileSystem.writeAsStringAsync(fileUri, rawBase64, {
          encoding: FileSystem.EncodingType.Base64,
        });
        if (await Sharing.isAvailableAsync()) {
          await Sharing.shareAsync(fileUri, {
            mimeType: "image/png",
            dialogTitle: data.title || "Export Report Image",
            UTI: "public.png",
          });
        }
      }
    } catch (err) {
      console.warn("WebView message handling error:", err);
    }
  };


  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" backgroundColor="#041632" />

      {/* Offline / Error Fallback */}
      {hasError ? (
        <View style={styles.errorContainer}>
          <Image
            source={require("./assets/icon.png")}
            style={styles.errorLogo}
            resizeMode="contain"
          />
          <Text style={styles.errorTitle}>Connection Error</Text>
          <Text style={styles.errorSubtitle}>
            Unable to reach the AuditCkDayo server. Please check your internet connection and try again.
          </Text>
          <TouchableOpacity style={styles.retryButton} onPress={handleRetry} activeOpacity={0.85}>
            <Text style={styles.retryButtonText}>RETRY CONNECTION</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <View style={styles.webviewWrapper}>
          <WebView
            ref={webViewRef}
            source={{ uri: APP_URL }}
            style={styles.webview}
            onLoadStart={() => setIsLoading(true)}
            onLoadEnd={() => setIsLoading(false)}
            onError={() => {
              setHasError(true);
              setIsLoading(false);
            }}
            onNavigationStateChange={(navState) => setCanGoBack(navState.canGoBack)}
            allowsInlineMediaPlayback
            mediaPlaybackRequiresUserAction={false}
            allowsBackForwardNavigationGestures
            javaScriptEnabled
            domStorageEnabled
            pullToRefreshEnabled
            cacheEnabled
            sharedCookiesEnabled
            thirdPartyCookiesEnabled
            onMessage={handleMessage}
          />

          {/* Initial Loading Overlay with Makbie Logo */}
          {isLoading && (
            <View style={styles.loadingOverlay}>
              <Image
                source={require("./assets/icon.png")}
                style={styles.loadingLogo}
                resizeMode="contain"
              />
              <ActivityIndicator size="large" color="#041632" style={{ marginTop: 16 }} />
              <Text style={styles.loadingText}>Loading Makbie Companies...</Text>
            </View>
          )}
        </View>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#041632",
  },
  webviewWrapper: {
    flex: 1,
    position: "relative",
    backgroundColor: "#ffffff",
  },
  webview: {
    flex: 1,
    backgroundColor: "#ffffff",
  },
  loadingOverlay: {
    position: "absolute",
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: "#ffffff",
    alignItems: "center",
    justifyContent: "center",
    zIndex: 10,
  },
  loadingLogo: {
    width: 140,
    height: 140,
  },
  loadingText: {
    marginTop: 12,
    fontSize: 13,
    fontWeight: "700",
    color: "#041632",
    textTransform: "uppercase",
    letterSpacing: 0.5,
  },
  errorContainer: {
    flex: 1,
    backgroundColor: "#ffffff",
    alignItems: "center",
    justifyContent: "center",
    padding: 32,
  },
  errorLogo: {
    width: 120,
    height: 120,
    marginBottom: 20,
  },
  errorTitle: {
    fontSize: 20,
    fontWeight: "800",
    color: "#041632",
    marginBottom: 8,
  },
  errorSubtitle: {
    fontSize: 13,
    color: "#64748b",
    textAlign: "center",
    lineHeight: 18,
    marginBottom: 24,
    maxWidth: 280,
  },
  retryButton: {
    backgroundColor: "#041632",
    paddingVertical: 14,
    paddingHorizontal: 28,
    borderRadius: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  retryButtonText: {
    color: "#ffffff",
    fontSize: 12,
    fontWeight: "800",
    letterSpacing: 0.8,
  },
});
