import React, { useState, useEffect } from 'react';
import { settingsService } from '../services/api';

export const SettingsPage: React.FC = () => {
  const [clientId, setClientId] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [status, setStatus] = useState('');

  useEffect(() => {
    settingsService.getSettings()
      .then(data => {
        setClientId(data.clientId);
        setAccessToken(data.accessToken);
      })
      .catch(() => {});
  }, []);

  const handleSave = async () => {
    try {
      await settingsService.saveSettings({ clientId, accessToken });
      setStatus('Settings saved successfully!');
    } catch (err) {
      setStatus('Error saving settings.');
    }
  };

  return (
    <div className="p-6 max-w-md mx-auto bg-gray-900 text-white rounded-lg shadow-xl mt-10">
      <h2 className="text-2xl font-bold mb-4">Dhan API Settings</h2>
      <div className="mb-4">
        <label className="block text-sm mb-1">Client ID</label>
        <input
          className="w-full p-2 bg-gray-800 border border-gray-700 rounded"
          value={clientId}
          onChange={(e) => setClientId(e.target.value)}
        />
      </div>
      <div className="mb-6">
        <label className="block text-sm mb-1">Access Token</label>
        <textarea
          className="w-full p-2 bg-gray-800 border border-gray-700 rounded h-24"
          value={accessToken}
          onChange={(e) => setAccessToken(e.target.value)}
        />
      </div>
      <button
        className="w-full bg-blue-600 hover:bg-blue-700 p-2 rounded font-bold transition"
        onClick={handleSave}
      >
        Save Settings
      </button>
      {status && <p className="mt-4 text-center text-sm text-blue-400">{status}</p>}
    </div>
  );
};
