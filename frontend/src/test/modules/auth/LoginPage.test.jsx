import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import LoginPage from '../../../modules/auth/LoginPage'

// Override global mocks for this test file
vi.mock('../../../stores/authStore', () => ({
  default: vi.fn(() => ({
    setAuth: vi.fn(),
  })),
}))

vi.mock('../../../services/authService', () => ({
  default: {
    login: vi.fn(),
  },
}))

vi.mock('react-hot-toast', () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

const renderLogin = () =>
  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>
  )

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders login form with username and password fields', () => {
    renderLogin()

    expect(screen.getByLabelText(/usuario/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/contraseña/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /iniciar sesión/i })).toBeInTheDocument()
  })

  it('renders app title', () => {
    renderLogin()

    expect(screen.getByText('Walos')).toBeInTheDocument()
    expect(screen.getByText(/sistema de gestión/i)).toBeInTheDocument()
  })

  it('updates input values on change', () => {
    renderLogin()

    const usernameInput = screen.getByLabelText(/usuario/i)
    const passwordInput = screen.getByLabelText(/contraseña/i)

    fireEvent.change(usernameInput, { target: { value: 'admin@mibar.com' } })
    fireEvent.change(passwordInput, { target: { value: 'admin123' } })

    expect(usernameInput.value).toBe('admin@mibar.com')
    expect(passwordInput.value).toBe('admin123')
  })

  it('shows dev credentials hint', () => {
    renderLogin()

    expect(screen.getByText(/admin@mibar.com/)).toBeInTheDocument()
  })

  it('toggles password visibility', () => {
    renderLogin()

    const passwordInput = screen.getByLabelText(/contraseña/i)
    expect(passwordInput.type).toBe('password')

    // Find and click the eye toggle button
    const toggleButtons = screen.getAllByRole('button')
    const eyeButton = toggleButtons.find(b => b.type === 'button')
    fireEvent.click(eyeButton)

    expect(passwordInput.type).toBe('text')
  })

  it('calls toast.error when submitting empty form', async () => {
    const toast = (await import('react-hot-toast')).default
    renderLogin()

    const submitButton = screen.getByRole('button', { name: /iniciar sesión/i })
    fireEvent.click(submitButton)

    expect(toast.error).toHaveBeenCalledWith('Ingresa usuario y contraseña')
  })
})
