import { useState } from 'react'
import { Dashboard } from './pages/Dashboard'
import { SettingsPage } from './pages/SettingsPage'
import './App.css'

function App() {
  const [page, setPage] = useState<'dashboard' | 'settings'>('dashboard')

  return (
    <div className="min-h-screen bg-[#131722]">
      <nav className="fixed top-4 right-4 z-50 flex gap-2">
        <button
          onClick={() => setPage('dashboard')}
          className={`px-3 py-1 rounded text-xs ${page === 'dashboard' ? 'bg-blue-600' : 'bg-gray-700'}`}
        >
          Dashboard
        </button>
        <button
          onClick={() => setPage('settings')}
          className={`px-3 py-1 rounded text-xs ${page === 'settings' ? 'bg-blue-600' : 'bg-gray-700'}`}
        >
          Settings
        </button>
      </nav>

      {page === 'dashboard' ? <Dashboard /> : <SettingsPage />}
    </div>
  )
}

export default App
