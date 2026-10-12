import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';
import App from './App';
const menu=[{id:1,name:'Rice bowl',category:'Meals',description:'Fresh lunch',price:550,imageUrl:'/food-placeholder.svg',preparationMinutes:10,isVegetarian:true},{id:2,name:'Iced coffee',category:'Drinks',description:'Cold brew',price:300,imageUrl:'/food-placeholder.svg',preparationMinutes:5,isVegetarian:true}];
afterEach(()=>{cleanup();vi.unstubAllGlobals();});
describe('CampusEats menu',()=>{
 it('fetches the API, filters categories and searches dishes',async()=>{
  const fetch=vi.fn().mockResolvedValue({ok:true,json:async()=>menu});vi.stubGlobal('fetch',fetch);
  render(<App/>);expect(await screen.findByText('Rice bowl')).toBeInTheDocument();expect(fetch.mock.calls[0][0]).toMatch(/\/api\/menu$/);
  fireEvent.click(screen.getByRole('button',{name:'Drinks'}));expect(screen.queryByText('Rice bowl')).not.toBeInTheDocument();expect(screen.getByText('Iced coffee')).toBeInTheDocument();
  fireEvent.change(screen.getByRole('textbox',{name:'Search menu'}),{target:{value:'no match'}});expect(screen.getByText(/No dishes found/)).toBeInTheDocument();
 });
 it('shows an error and retries without displaying fake menu data',async()=>{
  vi.stubGlobal('fetch',vi.fn().mockResolvedValueOnce({ok:false}).mockResolvedValueOnce({ok:true,json:async()=>menu}));
  render(<App/>);expect(await screen.findByRole('alert')).toBeInTheDocument();fireEvent.click(screen.getByRole('button',{name:'Try again'}));expect(await screen.findByText('Rice bowl')).toBeInTheDocument();
 });
});
