export const API_URL = (import.meta.env.VITE_API_URL || 'http://localhost:8080').replace(/\/+$/, '');
export async function getMenu(signal) {
  const response = await fetch(`${API_URL}/api/menu`, { signal });
  if (!response.ok) throw new Error('The menu is unavailable. Please try again shortly.');
  const items = await response.json();
  if (!Array.isArray(items)) throw new Error('The menu response was unexpected.');
  return items;
}
