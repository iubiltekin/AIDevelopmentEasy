import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { PipelineUpdateMessage } from '../types';

const HUB_URL = '/hubs/pipeline';

export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [lastUpdate, setLastUpdate] = useState<PipelineUpdateMessage | null>(null);
  const [requirementListChanged, setRequirementListChanged] = useState(0);

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

    connection.on('RequirementListChanged', () => {
      console.log('Requirement list changed');
      setRequirementListChanged(prev => prev + 1);
    });

    connection.on('RequirementCompleted', (requirementId: string) => {
      console.log('Requirement completed:', requirementId);
      setRequirementListChanged(prev => prev + 1);
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

  const subscribeToRequirement = useCallback(async (requirementId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToRequirement', requirementId);
    }
  }, []);

  const unsubscribeFromRequirement = useCallback(async (requirementId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromRequirement', requirementId);
    }
  }, []);

  return {
    isConnected,
    lastUpdate,
    requirementListChanged,
    subscribeToRequirement,
    unsubscribeFromRequirement
  };
}
