import useDeviceStore from '../../../stores/deviceStore';

const useScale = () => {
  const scale = useDeviceStore((state) => state.scale);
  const connect = useDeviceStore((state) => state.connectScale);
  const disconnect = useDeviceStore((state) => state.disconnectScale);
  const config = useDeviceStore((state) => state.scaleConfig);

  return {
    ...scale,
    config,
    connect,
    disconnect,
  };
};

export default useScale;
