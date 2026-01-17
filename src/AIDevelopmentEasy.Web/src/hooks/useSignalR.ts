import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { PipelineUpdateMessage } from '../types';

const HUB_URL = '/hubs/pipeline';

export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastUpdate, setLastUpdate] = useState<PipelineUpdateMessage | null>(null);
  const [storyListChanged, setStoryListChanged] = useState(0);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    connection.on('PipelineUpdate', (message: PipelineUpdateMessage) => {
      console.log('Pipeline update:', message);
      setLastUpdate(message);
    });

    connection.on('StoryListChanged', () => {
      console.log('Story list changed');
      setStoryListChanged(prev => prev + 1);
    });

    connection.on('StoryCompleted', (storyId: string) => {
      console.log('Story completed:', storyId);
      setStoryListChanged(prev => prev + 1);
    });

    connection.onclose(() => {
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      setIsConnected(true);
    });

    connection
      .start()
      .then(() => {
        setIsConnected(true);
        // Subscribe to all updates by default
        connection.invoke('SubscribeToAll').catch(console.error);
      })
      .catch(console.error);

    return () => {
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
