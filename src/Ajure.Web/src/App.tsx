import { Navigate, Route, BrowserRouter as Router, Routes } from 'react-router-dom'
import { Landing } from './routes/Landing'
import { Projects } from './routes/Projects'
import { NewProject } from './routes/NewProject'
import { Decisions } from './routes/Decisions'
import { Run } from './routes/Run'
import { Workspace } from './routes/Workspace'
import { ProviderSettings } from './routes/ProviderSettings'

export function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Landing />} />
        <Route path="/projects" element={<Projects />} />
        <Route path="/projects/new" element={<NewProject />} />
        <Route path="/projects/:id/decisions" element={<Decisions />} />
        <Route path="/projects/:id/run/:jobId" element={<Run />} />
        <Route path="/projects/:id/workspace" element={<Workspace />} />
        <Route path="/settings/providers" element={<ProviderSettings />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Router>
  )
}
