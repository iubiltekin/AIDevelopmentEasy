import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { PipelineUpdateMessage } from '../types';

const HUB_URL = '/hubs/pipeline';
const HEARTBEAT_INTERVAL_MS = 3000;
const RECONNECT_INTERVAL_MS = 2000;
const HEALTH_TIMEOUT_MS = 2000;

export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastUpdate, setLastUpdate] = useState<PipelineUpdateMessage | null>(null);
  const [storyListChanged, setStoryListChanged] = useState(0);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('PipelineUpdate', (message: PipelineUpdateMessage) => {
      setLastUpdate(message);
    });

    connection.on('StoryListChanged', () => {
      setStoryListChanged(prev => prev + 1);
    });

    connection.on('StoryCompleted', (_storyId: string) => {
      setStoryListChanged(prev => prev + 1);
    });

    connection.onclose(() => {
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
      connection.invoke('SubscribeToAll').catch(() => { });
    });

    const startConnection = () => {
      connection.start()
        .then(() => {
          setIsConnected(true);
          connection.invoke('SubscribeToAll').catch(() => { });
        })
        .catch(() => { });
    };

    startConnection();

    // Heartbeat: detect server death quickly (UI shows Disconnected sooner)
    const heartbeatId = setInterval(async () => {
      if (connectionRef.current?.state !== signalR.HubConnectionState.Connected) return;
      try {
        const c = new AbortController();
        const t = setTimeout(() => c.abort(), HEALTH_TIMEOUT_MS);
        const res = await fetch('/health', { signal: c.signal });
        clearTimeout(t);
        if (!res.ok) {
          setIsConnected(false);
          connection.stop().catch(() => { });
        }
      } catch {
        setIsConnected(false);
        connection.stop().catch(() => { });
      }
    }, HEARTBEAT_INTERVAL_MS);

    // Reconnect: when disconnected, try to connect again so UI shows Connected soon after server is back
    const reconnectId = setInterval(() => {
      const state = connectionRef.current?.state;
      if (state === signalR.HubConnectionState.Disconnected) {
        startConnection();
      }
    }, RECONNECT_INTERVAL_MS);

    // When user returns to the tab/window, try reconnect immediately
    const onVisibility = () => {
      if (document.visibilityState === 'visible' && connectionRef.current?.state === signalR.HubConnectionState.Disconnected) {
        startConnection();
      }
    };
    document.addEventListener('visibilitychange', onVisibility);

    return () => {
      clearInterval(heartbeatId);
      clearInterval(reconnectId);
      document.removeEventListener('visibilitychange', onVisibility);
      connection.stop();
    };
  }, []);

  const subscribeToStory = useCallback(async (storyId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToStory', storyId);
    }
  }, []);

  const unsubscribeFromStory = useCallback(async (storyId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromStory', storyId);
    }
  }, []);

  return {
    isConnected,
    lastUpdate,
    storyListChanged,
    subscribeToStory,
    unsubscribeFromStory
  };
}
